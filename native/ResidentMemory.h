#pragma once

// Non-real-time only. VirtualLock has no reference count, so DLL pages are
// acquired once per process and released only after the last locked APO.
namespace ResidentMemory {
struct Range { void* address; SIZE_T bytes; };
static SRWLOCK guard=SRWLOCK_INIT;
static ULONG users=0,count=0;
static Range ranges[64]={};
#ifdef CHANNELFLIP_TEST_HOOKS
static LONG failAt=0,attempts=0,held=0;
#endif
static bool Lock(void* address,SIZE_T bytes) {
#ifdef CHANNELFLIP_TEST_HOOKS
    if (++attempts==failAt) { SetLastError(ERROR_WORKING_SET_QUOTA); return false; }
#endif
    if (!VirtualLock(address,bytes)) return false;
#ifdef CHANNELFLIP_TEST_HOOKS
    ++held;
#endif
    return true;
}
static bool Unlock(void* address,SIZE_T bytes) {
    bool ok=VirtualUnlock(address,bytes)!=FALSE;
#ifdef CHANNELFLIP_TEST_HOOKS
    if(ok)--held;
#endif
    return ok;
}
static DWORD ReleaseRanges() {
    DWORD error=ERROR_SUCCESS; ULONG retained=0;
    for(ULONG i=0;i<count;++i) {
        const auto r=ranges[i];
        if(!Unlock(r.address,r.bytes)) {
            DWORD cause=GetLastError();
            if(cause!=ERROR_NOT_LOCKED) { ranges[retained++]=r; error=cause; }
        }
    }
    count=retained;return error;
}
static DWORD AcquireImage() {
    AcquireSRWLockExclusive(&guard);
    if (users) { ++users; ReleaseSRWLockExclusive(&guard); return ERROR_SUCCESS; }
    HMODULE module=nullptr;
    DWORD error=ReleaseRanges(); // Retry any failed release before acquiring a new lease.
    if (!error && !GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        reinterpret_cast<LPCWSTR>(&AcquireImage),&module)) error=GetLastError();
    if (!error) {
        auto base=reinterpret_cast<BYTE*>(module);
        auto nt=reinterpret_cast<IMAGE_NT_HEADERS64*>(base+reinterpret_cast<IMAGE_DOS_HEADER*>(base)->e_lfanew);
        BYTE* end=base+nt->OptionalHeader.SizeOfImage;
        for(BYTE* p=base;p<end;) {
            MEMORY_BASIC_INFORMATION info={};
            if(!VirtualQuery(p,&info,sizeof(info))) { error=GetLastError(); break; }
            SIZE_T bytes=info.RegionSize;
            if(p+bytes>end) bytes=end-p;
            if(info.State==MEM_COMMIT && !(info.Protect&(PAGE_NOACCESS|PAGE_GUARD))) {
                if(count==64) { error=ERROR_INSUFFICIENT_BUFFER; break; }
                if(!Lock(p,bytes)) { error=GetLastError(); break; }
                ranges[count++]={p,bytes};
            }
            p+=bytes;
        }
    }
    if(error) { ReleaseRanges(); }
    else users=1;
    ReleaseSRWLockExclusive(&guard); return error;
}
static DWORD ReleaseImage() {
    DWORD error=ERROR_SUCCESS;
    AcquireSRWLockExclusive(&guard);
    if(users && --users==0) error=ReleaseRanges();
    ReleaseSRWLockExclusive(&guard); return error;
}
}

#ifdef CHANNELFLIP_TEST_HOOKS
// Exported only from the separate test DLL, never from a packaged core.
extern "C" __declspec(dllexport) void __stdcall ChannelFlipTestFailLock(LONG at) { ResidentMemory::failAt=at; ResidentMemory::attempts=0; }
extern "C" __declspec(dllexport) LONG __stdcall ChannelFlipTestHeldLocks() { return ResidentMemory::held; }
#endif
