#include "apo-abi.h"
#include <string.h>
#include "ResidentMemory.h"

inline void* operator new(size_t,void* address) noexcept { return address; }

static LONG objects = 0;
static LONG serverLocks = 0;
static constexpr HRESULT Unsupported = (HRESULT)0x887d0003;

static bool Same(REFGUID a, REFGUID b) { return memcmp(&a,&b,sizeof(GUID)) == 0; }
static DWORD Layout(const CF_FORMAT& f) {
    // WAVEFORMATEX stereo has an implicit FL/FR order; multichannel needs a mask.
    if(!f.mask) return f.channels==1 ? 4u : f.channels==2 ? 3u : 0u;
    DWORD mask=f.mask,bits=0;
    if(mask&~0x3ffffu) return 0; // Only defined Windows speaker positions.
    for(DWORD value=mask;value;value&=value-1) ++bits;
    if(bits!=f.channels || (f.channels>=2 && (mask&3u)!=3u)) return 0;
    return mask;
}
static bool Valid(const CF_FORMAT& f) {
    return Same(f.type,CF_FLOAT) && f.bytes == 4 && f.validBits == 32 && f.channels >= 1
        && f.channels <= 18 && f.rate >= 8000 && f.rate <= 384000 && Layout(f)!=0;
}
static bool Compatible(const CF_FORMAT& a,const CF_FORMAT& b) {
    return Valid(a) && Valid(b) && a.channels == b.channels && a.rate == b.rate && Layout(a)==Layout(b);
}
static void CopySwap(float* output,const float* input,UINT32 frames,UINT32 channels,bool swap) {
    // Volatile byte copying keeps the RT path inside this pinned image and
    // preserves all float bit patterns, without an out-of-module CRT memcpy.
    if (output != input) {
        auto dst=reinterpret_cast<volatile BYTE*>(output);
        auto src=reinterpret_cast<const volatile BYTE*>(input);
        for(size_t i=0,n=(size_t)frames*channels*sizeof(float);i<n;++i)dst[i]=src[i];
    }
    if (!swap || channels < 2) return;
    for (UINT32 f=0;f<frames;f++) {
        float* p=output+(size_t)f*channels;
        float left=p[0]; p[0]=p[1]; p[1]=left;
    }
}

class ChannelFlip final : public CFApo,public CFApoConfig,public CFApoRT,public CFApoEffects {
    class InnerUnknown final : public IUnknown {
        ChannelFlip* owner;
    public:
        explicit InnerUnknown(ChannelFlip* value) : owner(value) {}
        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** value) override { return owner->InnerQueryInterface(iid,value); }
        ULONG STDMETHODCALLTYPE AddRef() override { return owner->InnerAddRef(); }
        ULONG STDMETHODCALLTYPE Release() override { return owner->InnerRelease(); }
    } inner;
    IUnknown* controller;
    LONG refs=1;
    bool initialized=false,locked=false;
    bool resident=false;
    UINT32 channels=0,maxFrames=0,processId=0;
    CF_STATE* state=nullptr;
    CFApo* child=nullptr;
    CFApoConfig* childConfig=nullptr;
    CFApoRT* childRT=nullptr;
    CFApoEffects* childEffects=nullptr;

    HRESULT LockMemory() {
        DWORD error=ResidentMemory::AcquireImage();
        if(error) return HRESULT_FROM_WIN32(error);
        if(!ResidentMemory::Lock(this,sizeof(*this))) { error=GetLastError(); ResidentMemory::ReleaseImage(); return HRESULT_FROM_WIN32(error); }
        if(state && !ResidentMemory::Lock(state,4096)) {
            error=GetLastError(); ResidentMemory::Unlock(this,sizeof(*this)); ResidentMemory::ReleaseImage(); return HRESULT_FROM_WIN32(error);
        }
        resident=true; return S_OK;
    }
    void UnlockMemory() {
        if(!resident)return;
        DWORD error=ERROR_SUCCESS;
        if(state && !ResidentMemory::Unlock(state,4096))error=GetLastError();
        if(!ResidentMemory::Unlock(this,sizeof(*this)))error=GetLastError();
        DWORD imageError=ResidentMemory::ReleaseImage(); if(imageError)error=imageError;
        resident=false;
        if(state && error)InterlockedExchange(&state->error,HRESULT_FROM_WIN32(error));
    }

    void ResetChild() {
        if (childEffects) childEffects->Release();
        if (childRT) childRT->Release();
        if (childConfig) childConfig->Release();
        if (child) child->Release();
        childEffects=nullptr; childRT=nullptr; childConfig=nullptr; child=nullptr;
    }
    HRESULT CheckFormat(CFMediaType* opposite,CFMediaType* requested,CFMediaType** supported,bool input) {
        if (!supported || !requested) return E_POINTER;
        *supported=nullptr;
        CF_FORMAT a={},b={};
        HRESULT hr=requested->GetUncompressedAudioFormat(&a);
        if (FAILED(hr)) return hr;
        if (!Valid(a)) return Unsupported;
        if (opposite) {
            hr=opposite->GetUncompressedAudioFormat(&b);
            if (FAILED(hr)) return hr;
            if (!Compatible(a,b)) return Unsupported;
        }
        if (child) return input ? child->IsInputFormatSupported(opposite,requested,supported)
                                 : child->IsOutputFormatSupported(opposite,requested,supported);
        requested->AddRef(); *supported=requested; return S_OK;
    }
public:
    explicit ChannelFlip(IUnknown* outer) : inner(this),controller(outer ? outer : &inner) { InterlockedIncrement(&objects); }
    ~ChannelFlip() {
        if (locked && childConfig) childConfig->UnlockForProcess();
        UnlockMemory();
        ResetChild();
        if (state) UnmapViewOfFile(state);
        InterlockedDecrement(&objects);
    }
    HRESULT InnerQueryInterface(REFIID iid,void** value) {
        if (!value) return E_POINTER;
        *value=nullptr;
        if (Same(iid,IID_IUnknown)) { *value=static_cast<IUnknown*>(&inner); InnerAddRef(); return S_OK; }
        if (Same(iid,CF_IID_APO)) *value=static_cast<CFApo*>(this);
        else if (Same(iid,CF_IID_CFG)) *value=static_cast<CFApoConfig*>(this);
        else if (Same(iid,CF_IID_RT)) *value=static_cast<CFApoRT*>(this);
        else if (Same(iid,CF_IID_FX) || Same(iid,CF_IID_FX2)) *value=static_cast<CFApoEffects*>(this);
        else return E_NOINTERFACE;
        AddRef(); return S_OK;
    }
    ULONG InnerAddRef() { return InterlockedIncrement(&refs); }
    ULONG InnerRelease() { ULONG count=InterlockedDecrement(&refs); if (!count) { this->~ChannelFlip(); VirtualFree(this,0,MEM_RELEASE); } return count; }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** value) override { return controller->QueryInterface(iid,value); }
    ULONG STDMETHODCALLTYPE AddRef() override { return controller->AddRef(); }
    ULONG STDMETHODCALLTYPE Release() override { return controller->Release(); }
    HRESULT STDMETHODCALLTYPE Reset() override { return child ? child->Reset() : S_OK; }
    HRESULT STDMETHODCALLTYPE GetLatency(HNSTIME* value) override {
        if (!value) return E_POINTER;
        *value=0; return child ? child->GetLatency(value) : S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetRegistrationProperties(CF_REG_PROPERTIES** value) override {
        if (!value) return E_POINTER;
        *value=(CF_REG_PROPERTIES*)CoTaskMemAlloc(sizeof(CF_REG_PROPERTIES));
        if (!*value) return E_OUTOFMEMORY;
        auto p=*value; memset(p,0,sizeof(*p)); p->clsid=CF_CLSID; p->flags=14;
        lstrcpyW(p->name,L"Channel Flip native stereo swap"); lstrcpyW(p->copyright,L"Channel Flip 2026");
        p->major=2; p->minor=0; p->minInputs=p->maxInputs=p->minOutputs=p->maxOutputs=1;
        p->maxInstances=0xffffffff; p->numInterfaces=1; p->interfaces[0]=CF_IID_APO;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE Initialize(UINT32 size,BYTE* bytes) override {
        if (initialized) return (HRESULT)0x887d0001;
        if (!bytes || size<sizeof(CF_INIT) || size>4096) return E_INVALIDARG;
        ResetChild();
        if (state) { UnmapViewOfFile(state); state=nullptr; }
        auto init=(CF_INIT*)bytes;
        if (!init->endpoint) return E_POINTER;
        PROPVARIANT value={};
        HRESULT hr=init->endpoint->GetValue(CF_ENDPOINT_GUID,&value);
        if (FAILED(hr)) return hr;
        GUID endpointGuid={};
        if (value.vt!=VT_LPWSTR || !value.pwszVal || FAILED(CLSIDFromString(value.pwszVal,&endpointGuid))) {
            PropVariantClear(&value); return E_INVALIDARG;
        }
        WCHAR guidText[40]={},keyPath[160]={};
        StringFromGUID2(endpointGuid,guidText,40);
        lstrcpyW(keyPath,L"SOFTWARE\\ChannelFlip\\Devices\\"); lstrcatW(keyPath,guidText);
        PropVariantClear(&value);
        HKEY key=nullptr;
        LONG error=RegOpenKeyExW(HKEY_LOCAL_MACHINE,keyPath,0,KEY_READ|KEY_WOW64_64KEY,&key);
        WCHAR statePath[1024]={},childClsid[80]={};
        if (error==ERROR_SUCCESS) {
            DWORD length=sizeof(statePath);
            RegGetValueW(key,nullptr,L"StatePath",RRF_RT_REG_SZ,nullptr,statePath,&length);
            length=sizeof(childClsid);
            RegGetValueW(key,nullptr,L"ChildClsid",RRF_RT_REG_SZ,nullptr,childClsid,&length);
            RegCloseKey(key);
        }
        if (statePath[0]) {
            HANDLE file=CreateFileW(statePath,GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,nullptr);
            if (file!=INVALID_HANDLE_VALUE) {
                LARGE_INTEGER length={};
                if (GetFileSizeEx(file,&length) && length.QuadPart>=4096) {
                    HANDLE mapping=CreateFileMappingW(file,nullptr,PAGE_READWRITE,0,4096,nullptr);
                    if (mapping) { state=(CF_STATE*)MapViewOfFile(mapping,FILE_MAP_READ|FILE_MAP_WRITE,0,0,4096); CloseHandle(mapping); }
                }
                CloseHandle(file);
            }
        }
        if (state && (state->magic!=CF_MAGIC || state->version!=1)) { UnmapViewOfFile(state); state=nullptr; }
        // A missing control file falls back to unchanged audio, never to silence.
        if (state) { InterlockedIncrement(&state->loads); InterlockedExchange(&state->error,0); }
        GUID childGuid={};
        if (childClsid[0] && SUCCEEDED(CLSIDFromString(childClsid,&childGuid)) && !Same(childGuid,GUID_NULL) && !Same(childGuid,CF_CLSID)) {
            hr=CoCreateInstance(childGuid,nullptr,CLSCTX_INPROC_SERVER,CF_IID_APO,(void**)&child);
            if (SUCCEEDED(hr)) hr=child->QueryInterface(CF_IID_CFG,(void**)&childConfig);
            if (SUCCEEDED(hr)) hr=child->QueryInterface(CF_IID_RT,(void**)&childRT);
            if (SUCCEEDED(hr)) {
                child->QueryInterface(CF_IID_FX2,(void**)&childEffects);
                BYTE* childInit=(BYTE*)CoTaskMemAlloc(size);
                if (!childInit) hr=E_OUTOFMEMORY;
                else {
                    memcpy(childInit,bytes,size); ((CF_INIT*)childInit)->base.clsid=childGuid;
                    hr=child->Initialize(size,childInit); CoTaskMemFree(childInit);
                }
            }
            if (FAILED(hr)) { if (state) InterlockedExchange(&state->error,hr); ResetChild(); return hr; }
        }
        processId=GetCurrentProcessId(); initialized=true; return S_OK;
    }
    HRESULT STDMETHODCALLTYPE IsInputFormatSupported(CFMediaType* opposite,CFMediaType* requested,CFMediaType** supported) override { return CheckFormat(opposite,requested,supported,true); }
    HRESULT STDMETHODCALLTYPE IsOutputFormatSupported(CFMediaType* opposite,CFMediaType* requested,CFMediaType** supported) override { return CheckFormat(opposite,requested,supported,false); }
    HRESULT STDMETHODCALLTYPE GetInputChannelCount(UINT32* value) override {
        if (!value) return E_POINTER;
        *value=channels; return channels ? S_OK : (HRESULT)0x887d0002;
    }
    HRESULT STDMETHODCALLTYPE LockForProcess(UINT32 nIn,CF_DESCRIPTOR** inputs,UINT32 nOut,CF_DESCRIPTOR** outputs) override {
        if (locked) return (HRESULT)0x887d000a;
        if (!initialized || nIn!=1 || nOut!=1 || !inputs || !outputs || !inputs[0] || !outputs[0] || !inputs[0]->format || !outputs[0]->format) return E_INVALIDARG;
        CF_FORMAT in={},out={};
        HRESULT hr=inputs[0]->format->GetUncompressedAudioFormat(&in);
        if (FAILED(hr)) return hr;
        hr=outputs[0]->format->GetUncompressedAudioFormat(&out);
        if (FAILED(hr)) return hr;
        if (!Compatible(in,out) || inputs[0]->maxFrames==0 || inputs[0]->maxFrames>1048576 || outputs[0]->maxFrames<inputs[0]->maxFrames) return Unsupported;
        hr=LockMemory();
        if(FAILED(hr)) { if(state)InterlockedExchange(&state->error,hr); return hr; }
        if (childConfig) { hr=childConfig->LockForProcess(nIn,inputs,nOut,outputs); if (FAILED(hr)) { UnlockMemory(); if(state)InterlockedExchange(&state->error,hr); return hr; } }
        channels=in.channels; maxFrames=inputs[0]->maxFrames; locked=true;
        if (state) { InterlockedExchange(&state->error,0); InterlockedExchange(&state->channels,channels); InterlockedExchange(&state->rate,(LONG)in.rate); }
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE UnlockForProcess() override {
        if (!locked) return (HRESULT)0x887d0006;
        HRESULT hr=childConfig ? childConfig->UnlockForProcess() : S_OK;
        locked=false; UnlockMemory(); return hr;
    }
    void STDMETHODCALLTYPE APOProcess(UINT32 nIn,APO_CONNECTION_PROPERTY** inputs,UINT32 nOut,APO_CONNECTION_PROPERTY** outputs) override {
        // Real-time path: bounded copies/swaps and atomic memory operations only.
        if (nOut!=1 || !outputs || !outputs[0]) return;
        auto output=outputs[0];
        if (!locked || nIn!=1 || !inputs || !inputs[0] || inputs[0]->u32ValidFrameCount>maxFrames) {
            output->u32ValidFrameCount=0; output->u32BufferFlags=BUFFER_INVALID; return;
        }
        auto input=inputs[0];
        if (input->u32BufferFlags==BUFFER_VALID && (!input->pBuffer || !output->pBuffer)) {
            output->u32ValidFrameCount=0; output->u32BufferFlags=BUFFER_INVALID; return;
        }
        if (childRT) childRT->APOProcess(nIn,inputs,nOut,outputs);
        else {
            output->u32ValidFrameCount=input->u32ValidFrameCount; output->u32BufferFlags=input->u32BufferFlags;
            if (input->u32BufferFlags==BUFFER_VALID && input->pBuffer && output->pBuffer)
                CopySwap((float*)output->pBuffer,(const float*)input->pBuffer,input->u32ValidFrameCount,channels,false);
        }
        if (output->u32ValidFrameCount>maxFrames) { output->u32ValidFrameCount=0; output->u32BufferFlags=BUFFER_INVALID; return; }
        bool enabled=state && state->enabled!=0;
        if (output->u32BufferFlags==BUFFER_VALID && output->pBuffer && enabled && channels>=2) {
            CopySwap((float*)output->pBuffer,(const float*)output->pBuffer,output->u32ValidFrameCount,channels,true);
            InterlockedAdd64(&state->swappedFrames,output->u32ValidFrameCount);
        }
        if (state) { InterlockedAdd64(&state->frames,output->u32ValidFrameCount); InterlockedExchange(&state->lastProcess,processId); }
    }
    UINT32 STDMETHODCALLTYPE CalcInputFrames(UINT32 value) override { return childRT ? childRT->CalcInputFrames(value) : value; }
    UINT32 STDMETHODCALLTYPE CalcOutputFrames(UINT32 value) override { return childRT ? childRT->CalcOutputFrames(value) : value; }
    HRESULT STDMETHODCALLTYPE GetEffectsList(GUID** effects,UINT* count,HANDLE changed) override {
        if (!effects || !count) return E_POINTER;
        if (childEffects) return childEffects->GetEffectsList(effects,count,changed);
        *effects=nullptr; *count=0; return S_OK;
    }
};

class Factory final : public IClassFactory {
    LONG refs=1;
public:
    Factory() { InterlockedIncrement(&objects); }
    ~Factory() { InterlockedDecrement(&objects); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** value) override {
        if (!value) return E_POINTER; *value=nullptr;
        if (!Same(iid,IID_IUnknown) && !Same(iid,IID_IClassFactory)) return E_NOINTERFACE;
        *value=static_cast<IClassFactory*>(this); AddRef(); return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs); }
    ULONG STDMETHODCALLTYPE Release() override { ULONG count=InterlockedDecrement(&refs); if (!count) { this->~Factory(); VirtualFree(this,0,MEM_RELEASE); } return count; }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer,REFIID iid,void** value) override {
        if (!value) return E_POINTER; *value=nullptr;
        if (outer && !Same(iid,IID_IUnknown)) return E_NOINTERFACE;
        // A dedicated VirtualAlloc region cannot share locked pages with another
        // object or an allocator's metadata. No per-instance unlock can unpin it.
        void* memory=VirtualAlloc(nullptr,sizeof(ChannelFlip),MEM_RESERVE|MEM_COMMIT,PAGE_READWRITE);
        if(!memory)return E_OUTOFMEMORY;
        ChannelFlip* instance=new(memory) ChannelFlip(outer);
        HRESULT hr=instance->InnerQueryInterface(iid,value); instance->InnerRelease(); return hr;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override { if (lock) InterlockedIncrement(&serverLocks); else InterlockedDecrement(&serverLocks); return S_OK; }
};

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid,REFIID iid,void** value) {
    if (!value) return E_POINTER; *value=nullptr;
    if (!Same(clsid,CF_CLSID)) return CLASS_E_CLASSNOTAVAILABLE;
    void* memory=VirtualAlloc(nullptr,sizeof(Factory),MEM_RESERVE|MEM_COMMIT,PAGE_READWRITE);
    if(!memory)return E_OUTOFMEMORY;
    Factory* factory=new(memory) Factory();
    HRESULT hr=factory->QueryInterface(iid,value); factory->Release(); return hr;
}
extern "C" HRESULT __stdcall DllCanUnloadNow() { return objects==0 && serverLocks==0 ? S_OK : S_FALSE; }
