#include <psapi.h>
#include <stdlib.h>

static int CompareDurations(const void* a,const void* b) {
    double x=*(const double*)a,y=*(const double*)b;return x<y?-1:x>y?1:0;
}
// Measurements belong to this isolated host. They do not claim to measure an
// AudioDG thread's faults or physical headphone glitches; use ETW for those.
static void RunStress(Instance& instance,const WCHAR* reportPath) {
    constexpr UINT Frames=480,Samples=5000;
    auto input=(float*)VirtualAlloc(nullptr,Frames*2*sizeof(float),MEM_RESERVE|MEM_COMMIT,PAGE_READWRITE);
    auto output=(float*)VirtualAlloc(nullptr,Frames*2*sizeof(float),MEM_RESERVE|MEM_COMMIT,PAGE_READWRITE);
    auto pressure=(volatile BYTE*)VirtualAlloc(nullptr,64*1024*1024,MEM_RESERVE|MEM_COMMIT,PAGE_READWRITE);
    auto durations=(double*)VirtualAlloc(nullptr,Samples*sizeof(double),MEM_RESERVE|MEM_COMMIT,PAGE_READWRITE);
    if(!input||!output||!pressure||!durations) { Check(false,"F03 pressure harness allocations");return; }
    bool inputLocked=VirtualLock(input,Frames*2*sizeof(float))!=FALSE,outputLocked=VirtualLock(output,Frames*2*sizeof(float))!=FALSE;
    Check(inputLocked&&outputLocked,"F03 isolated host makes its own audio buffers resident");
    for(UINT i=0;i<Frames*2;++i) input[i]=(float)i;
    LARGE_INTEGER frequency;QueryPerformanceFrequency(&frequency);
    PROCESS_MEMORY_COUNTERS before={},after={};before.cb=after.cb=sizeof(before);
    GetProcessMemoryInfo(GetCurrentProcess(),&before,sizeof(before));
    DWORD initialFaults=before.PageFaultCount,callbackWindowFaults=0,invalid=0,overruns=0,trims=0;
    double total=0;
    for(UINT i=0;i<Samples;++i) {
        if(i%100==0) {
            for(size_t page=0;page<64*1024*1024;page+=4096) pressure[page]=(BYTE)i;
            if(EmptyWorkingSet(GetCurrentProcess()))++trims;
        }
        LARGE_INTEGER start,end;
        GetProcessMemoryInfo(GetCurrentProcess(),&before,sizeof(before));
        QueryPerformanceCounter(&start);
        auto result=instance.Process(output,input,Frames);
        QueryPerformanceCounter(&end);
        GetProcessMemoryInfo(GetCurrentProcess(),&after,sizeof(after));
        callbackWindowFaults+=after.PageFaultCount-before.PageFaultCount;
        double ms=(end.QuadPart-start.QuadPart)*1000.0/frequency.QuadPart;
        durations[i]=ms;total+=ms;if(ms>10.0)++overruns;
        if(result.u32BufferFlags!=BUFFER_VALID||result.u32ValidFrameCount!=Frames||output[0]!=1||output[1]!=0)++invalid;
    }
    qsort(durations,Samples,sizeof(double),CompareDurations);
    FILE* report=_wfopen(reportPath,L"wb");
    if(report) {
        fprintf(report,"{\n  \"scope\":\"isolated DLL harness; not AudioDG\",\n  \"samples\":%u,\n  \"framesPerCallback\":%u,\n  \"pressureBytes\":67108864,\n  \"workingSetTrims\":%lu,\n  \"processPageFaults\":%lu,\n  \"callbackWindowProcessPageFaults\":%lu,\n  \"meanMs\":%.6f,\n  \"p99Ms\":%.6f,\n  \"maxMs\":%.6f,\n  \"deadlineOverruns\":%lu,\n  \"invalidBuffers\":%lu,\n  \"physicalAudioGlitches\":null,\n  \"sleepWake\":\"not exercised\"\n}\n",
            Samples,Frames,trims,after.PageFaultCount-initialFaults,callbackWindowFaults,total/Samples,durations[Samples*99/100],durations[Samples-1],overruns,invalid);
        fclose(report);
    }
    Check(report!=nullptr&&trims>0,"F03 pressure report records faults, durations and deadline overruns");
    Check(invalid==0,"F03 all pressured callbacks preserve valid swapped buffers");
    if(inputLocked)VirtualUnlock(input,Frames*2*sizeof(float));if(outputLocked)VirtualUnlock(output,Frames*2*sizeof(float));
    VirtualFree(input,0,MEM_RELEASE);VirtualFree(output,0,MEM_RELEASE);VirtualFree((void*)pressure,0,MEM_RELEASE);VirtualFree(durations,0,MEM_RELEASE);
}
