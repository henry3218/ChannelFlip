// Exercise the shipped DLL through Microsoft's SDK interfaces, independently
// of the smaller ABI declarations used to build the production DLL.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include "audioenginebaseapo.h"
#include <stdio.h>
#include <string.h>
#include <stddef.h>

static const GUID Core = {0xf81b4c35,0x7458,0x4c1e,{0xb8,0x20,0x77,0x0a,0x91,0x4e,0xd4,0x36}};
static const GUID ApoId = {0xfd7f2b29,0x24d0,0x4b5c,{0xb1,0x77,0x59,0x2c,0x39,0xf9,0xca,0x10}};
static const GUID ConfigId = {0x0e5ed805,0xaba6,0x49c3,{0x8f,0x9a,0x2b,0x8c,0x88,0x9c,0x4f,0xa8}};
static const GUID RtId = {0x9e1d6a6d,0xddbc,0x4e95,{0xa4,0xc7,0xad,0x64,0xba,0x37,0x84,0x6c}};
static const GUID FxId = {0xbafe99d2,0x7436,0x44ce,{0x9e,0x0e,0x4d,0x89,0xaf,0xbf,0xff,0x56}};
static const GUID FloatId = {3,0,0x10,{0x80,0,0,0xaa,0,0x38,0x9b,0x71}};
static const WCHAR Endpoint[] = L"{fb240fa0-1d7b-479b-9442-00a7412eaa11}";
static const PROPERTYKEY EndpointKey = {{0x1da5d803,0xd492,0x4edd,{0x8c,0x23,0xe0,0xc0,0xff,0xee,0x7f,0x0e}},4};
static int passed=0,failed=0;
static void Check(bool ok,const char* name) { printf("%s %s\n",ok?"PASS":"FAIL",name); if(ok)passed++;else failed++; }
static bool Same(REFGUID a,REFGUID b) { return memcmp(&a,&b,sizeof(GUID))==0; }

class Properties : public IPropertyStore {
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID,void** p) override { if(!p)return E_POINTER;*p=this;AddRef();return S_OK; }
    ULONG STDMETHODCALLTYPE AddRef() override { return 2; }
    ULONG STDMETHODCALLTYPE Release() override { return 1; }
    HRESULT STDMETHODCALLTYPE GetCount(DWORD* p) override { *p=1; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetAt(DWORD,PROPERTYKEY* p) override { *p=EndpointKey; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetValue(REFPROPERTYKEY key,PROPVARIANT* p) override {
        memset(p,0,sizeof(*p)); if(!Same(key.fmtid,EndpointKey.fmtid)||key.pid!=4)return E_INVALIDARG;
        p->vt=VT_LPWSTR; p->pwszVal=(LPWSTR)CoTaskMemAlloc(sizeof(Endpoint)); memcpy(p->pwszVal,Endpoint,sizeof(Endpoint)); return S_OK;
    }
    HRESULT STDMETHODCALLTYPE SetValue(REFPROPERTYKEY,REFPROPVARIANT) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE Commit() override { return S_OK; }
};
class Format : public IAudioMediaType {
public:
    UNCOMPRESSEDAUDIOFORMAT data;
    WAVEFORMATEX wave={};
    Format(UINT channels=2,FLOAT rate=48000) : data{FloatId,channels,4,32,rate,channels==1?4u:channels==2?3u:channels==6?0x3fu:channels==8?0x63fu:0u} {}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID,void** p) override { if(!p)return E_POINTER;*p=this;AddRef();return S_OK; }
    ULONG STDMETHODCALLTYPE AddRef() override { return 2; }
    ULONG STDMETHODCALLTYPE Release() override { return 1; }
    HRESULT STDMETHODCALLTYPE IsCompressedFormat(BOOL* p) override { *p=FALSE;return S_OK; }
    HRESULT STDMETHODCALLTYPE IsEqual(IAudioMediaType*,DWORD* p) override { *p=0;return S_FALSE; }
    const WAVEFORMATEX* STDMETHODCALLTYPE GetAudioFormat() override { return &wave; }
    HRESULT STDMETHODCALLTYPE GetUncompressedAudioFormat(UNCOMPRESSEDAUDIOFORMAT* p) override { *p=data;return S_OK; }
};
struct State {
    DWORD magic,version; volatile LONG enabled,rate,channels,loads;
    volatile LONG64 frames,swapped; volatile LONG error,pid;
};
struct Environment {
    HKEY registry=nullptr; WCHAR keyName[120]={}; HANDLE file=INVALID_HANDLE_VALUE,mapping=nullptr; State* state=nullptr;
    bool Setup(const WCHAR* path) {
        wsprintfW(keyName,L"Software\\ChannelFlip.NativeTests.%lu",GetCurrentProcessId());
        if(RegCreateKeyExW(HKEY_CURRENT_USER,keyName,0,nullptr,REG_OPTION_VOLATILE,KEY_ALL_ACCESS,nullptr,&registry,nullptr))return false;
        // Only this process sees the private HKCU test tree as HKLM.
        if(RegOverridePredefKey(HKEY_LOCAL_MACHINE,registry))return false;
        WCHAR subkey[180];lstrcpyW(subkey,L"SOFTWARE\\ChannelFlip\\Devices\\");lstrcatW(subkey,Endpoint);
        HKEY device=nullptr; if(RegCreateKeyExW(registry,subkey,0,nullptr,REG_OPTION_VOLATILE,KEY_ALL_ACCESS,nullptr,&device,nullptr))return false;
        RegSetValueExW(device,L"StatePath",0,REG_SZ,(BYTE*)path,(lstrlenW(path)+1)*2);RegCloseKey(device);
        file=CreateFileW(path,GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);
        if(file==INVALID_HANDLE_VALUE)return false;
        SetFilePointer(file,4096,nullptr,FILE_BEGIN);SetEndOfFile(file);
        mapping=CreateFileMappingW(file,nullptr,PAGE_READWRITE,0,4096,nullptr);if(!mapping)return false;
        state=(State*)MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,4096);if(!state)return false;
        memset(state,0,4096);state->magic=0x50464c43;state->version=1;return true;
    }
    void Child(const WCHAR* value) {
        WCHAR subkey[180];lstrcpyW(subkey,L"SOFTWARE\\ChannelFlip\\Devices\\");lstrcatW(subkey,Endpoint);
        HKEY device=nullptr;RegOpenKeyExW(registry,subkey,0,KEY_SET_VALUE,&device);
        RegSetValueExW(device,L"ChildClsid",0,REG_SZ,(BYTE*)value,(lstrlenW(value)+1)*2);RegCloseKey(device);
    }
    ~Environment() {
        RegOverridePredefKey(HKEY_LOCAL_MACHINE,nullptr);
        if(state)UnmapViewOfFile(state);if(mapping)CloseHandle(mapping);if(file!=INVALID_HANDLE_VALUE)CloseHandle(file);
        if(registry)RegCloseKey(registry);if(keyName[0])RegDeleteTreeW(HKEY_CURRENT_USER,keyName);
    }
};
struct Instance {
    IAudioProcessingObject* apo=nullptr; IAudioProcessingObjectConfiguration* config=nullptr; IAudioProcessingObjectRT* rt=nullptr;
    HRESULT Create(IClassFactory* factory) {
        HRESULT hr=factory->CreateInstance(nullptr,ApoId,(void**)&apo);if(FAILED(hr))return hr;
        hr=apo->QueryInterface(ConfigId,(void**)&config);if(FAILED(hr))return hr;
        return apo->QueryInterface(RtId,(void**)&rt);
    }
    HRESULT Init(Properties& properties) {
        APOInitSystemEffects init={};init.APOInit.cbSize=sizeof(init);init.APOInit.clsid=Core;init.pAPOEndpointProperties=&properties;
        return apo->Initialize(sizeof(init),(BYTE*)&init);
    }
    HRESULT Lock(Format& input,Format& output,UINT32 max=8) {
        APO_CONNECTION_DESCRIPTOR a={},b={};a.pFormat=&input;b.pFormat=&output;a.u32MaxFrameCount=b.u32MaxFrameCount=max;
        APO_CONNECTION_DESCRIPTOR* in[]={&a};APO_CONNECTION_DESCRIPTOR* out[]={&b};return config->LockForProcess(1,in,1,out);
    }
    APO_CONNECTION_PROPERTY Process(float* output,const float* input,UINT32 frames,APO_BUFFER_FLAGS flags=BUFFER_VALID) {
        APO_CONNECTION_PROPERTY a={(UINT_PTR)input,frames,flags,0},b={(UINT_PTR)output,0,BUFFER_INVALID,0};
        APO_CONNECTION_PROPERTY* in[]={&a};APO_CONNECTION_PROPERTY* out[]={&b};rt->APOProcess(1,in,1,out);return b;
    }
    ~Instance(){if(config)config->UnlockForProcess();if(rt)rt->Release();if(config)config->Release();if(apo)apo->Release();}
};

static const GUID ChildId={0xabababab,0xabab,0xabab,{0xab,0xab,0xab,0xab,0xab,0xab,0xab,0xab}};
class Child : public IAudioProcessingObject,public IAudioProcessingObjectConfiguration,public IAudioProcessingObjectRT,public IAudioSystemEffects2 {
public:
    int initializations=0,locks=0,processes=0,unlocks=0;UINT channels=0;
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id,void** p) override {
        *p=nullptr;
        if(Same(id,ApoId)||Same(id,IID_IUnknown))*p=static_cast<IAudioProcessingObject*>(this);
        else if(Same(id,ConfigId))*p=static_cast<IAudioProcessingObjectConfiguration*>(this);
        else if(Same(id,RtId))*p=static_cast<IAudioProcessingObjectRT*>(this);
        else if(Same(id,FxId))*p=static_cast<IAudioSystemEffects2*>(this);
        else return E_NOINTERFACE;AddRef();return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override{return 2;} ULONG STDMETHODCALLTYPE Release() override{return 1;}
    HRESULT STDMETHODCALLTYPE Reset() override{return S_OK;}
    HRESULT STDMETHODCALLTYPE GetLatency(HNSTIME* p) override{*p=17;return S_OK;}
    HRESULT STDMETHODCALLTYPE GetRegistrationProperties(APO_REG_PROPERTIES**) override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE Initialize(UINT size,BYTE* data) override {
        if(size!=sizeof(APOInitSystemEffects)||!Same(((APOInitSystemEffects*)data)->APOInit.clsid,ChildId))return E_INVALIDARG;
        initializations++;return S_OK;
    }
    HRESULT STDMETHODCALLTYPE IsInputFormatSupported(IAudioMediaType*,IAudioMediaType* request,IAudioMediaType** supported) override{*supported=request;request->AddRef();return S_OK;}
    HRESULT STDMETHODCALLTYPE IsOutputFormatSupported(IAudioMediaType*,IAudioMediaType* request,IAudioMediaType** supported) override{*supported=request;request->AddRef();return S_OK;}
    HRESULT STDMETHODCALLTYPE GetInputChannelCount(UINT* p) override{*p=channels;return S_OK;}
    HRESULT STDMETHODCALLTYPE LockForProcess(UINT,APO_CONNECTION_DESCRIPTOR** in,UINT,APO_CONNECTION_DESCRIPTOR**) override{
        UNCOMPRESSEDAUDIOFORMAT format={};in[0]->pFormat->GetUncompressedAudioFormat(&format);channels=format.dwSamplesPerFrame;locks++;return S_OK;
    }
    HRESULT STDMETHODCALLTYPE UnlockForProcess() override{unlocks++;return S_OK;}
    void STDMETHODCALLTYPE APOProcess(UINT,APO_CONNECTION_PROPERTY** in,UINT,APO_CONNECTION_PROPERTY** out) override{
        out[0]->u32ValidFrameCount=in[0]->u32ValidFrameCount;out[0]->u32BufferFlags=in[0]->u32BufferFlags;
        if(in[0]->u32BufferFlags==BUFFER_VALID)for(UINT f=0;f<in[0]->u32ValidFrameCount*channels;f++)((float*)out[0]->pBuffer)[f]=((float*)in[0]->pBuffer)[f]*0.5f;
        processes++;
    }
    UINT STDMETHODCALLTYPE CalcInputFrames(UINT v) override{return v;} UINT STDMETHODCALLTYPE CalcOutputFrames(UINT v) override{return v;}
    HRESULT STDMETHODCALLTYPE GetEffectsList(GUID** p,UINT* count,HANDLE) override{*p=(GUID*)CoTaskMemAlloc(sizeof(GUID));**p=ChildId;*count=1;return S_OK;}
};
class ChildFactory : public IClassFactory {
public:
    Child child;
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID,void** p) override{*p=this;AddRef();return S_OK;}
    ULONG STDMETHODCALLTYPE AddRef() override{return 2;} ULONG STDMETHODCALLTYPE Release() override{return 1;}
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown*,REFIID id,void** p) override{return child.QueryInterface(id,p);}
    HRESULT STDMETHODCALLTYPE LockServer(BOOL) override{return S_OK;}
};

class OuterUnknown : public IUnknown {
public:
    LONG refs=1;
    IUnknown* inner=nullptr;
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** value) override {
        if(!value)return E_POINTER;
        *value=nullptr;
        if(Same(iid,IID_IUnknown)){*value=this;AddRef();return S_OK;}
        return inner ? inner->QueryInterface(iid,value) : E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override {return ++refs;}
    ULONG STDMETHODCALLTYPE Release() override {return --refs;}
};

#include "NativeStress.h"

int wmain(int argc,wchar_t** argv) {
    if(argc!=3&&argc!=4)return 64;
    CoInitializeEx(nullptr,COINIT_MULTITHREADED);
    HMODULE library=LoadLibraryW(argv[1]);if(!library){printf("LoadLibrary error %lu\n",GetLastError());return 2;}
    auto create=(HRESULT(WINAPI*)(REFCLSID,REFIID,void**))GetProcAddress(library,"DllGetClassObject");
    auto unload=(HRESULT(WINAPI*)())GetProcAddress(library,"DllCanUnloadNow");
    Check(create&&unload,"COM entry points exported");if(!create||!unload)return 2;
    Check(unload()==S_OK,"DLL initially unloadable");
    IClassFactory* factory=nullptr;
    Check(create(GUID_NULL,IID_IClassFactory,(void**)&factory)==CLASS_E_CLASSNOTAVAILABLE,"Unknown class rejected");
    Check(create(Core,IID_IClassFactory,(void**)&factory)==S_OK&&factory,"COM factory activates");if(!factory)return 2;
    {
        OuterUnknown outer;void* invalid=(void*)1;
        Check(factory->CreateInstance(&outer,ApoId,&invalid)==E_NOINTERFACE&&!invalid,"Aggregation requires initial IUnknown interface");
        Check(factory->CreateInstance(&outer,IID_IUnknown,(void**)&outer.inner)==S_OK&&outer.inner,"Audio host can aggregate the APO");
        if(outer.inner){
            IAudioProcessingObject* apo=nullptr;
            Check(outer.inner->QueryInterface(ApoId,(void**)&apo)==S_OK&&apo&&outer.refs==2,"Aggregated APO interfaces retain the controlling unknown");
            if(apo){
                IUnknown* identity=nullptr;
                Check(apo->QueryInterface(IID_IUnknown,(void**)&identity)==S_OK&&identity==&outer,"Aggregated APO preserves outer COM identity");
                if(identity)identity->Release();
                apo->Release();
            }
            Check(outer.refs==1,"Aggregated interface references balance");
            Check(outer.inner->Release()==0,"Nondelegating release destroys the aggregated APO");outer.inner=nullptr;
        }
    }
    {
        Environment env;if(!env.Setup(argv[2])){printf("Private test setup error %lu\n",GetLastError());return 2;}
        Properties properties; Format stereo,mono(1),surround(6),wrongRate(2,44100),integer;
        integer.data.guidFormatType.Data1=1; integer.data.dwBytesPerSampleContainer=2;integer.data.dwValidBitsPerSample=16;
        {
            Instance x;Check(x.Create(factory)==S_OK,"Required SDK COM interfaces available");
            Check(unload()==S_FALSE,"Live COM object prevents DLL unload");
            APO_REG_PROPERTIES* registration=nullptr;
            Check(x.apo->GetRegistrationProperties(&registration)==S_OK&&registration&&Same(registration->clsid,Core)&&registration->Flags==APO_FLAG_DEFAULT&&registration->u32NumAPOInterfaces==1,"SDK registration ABI and separate-buffer contract");
            CoTaskMemFree(registration);
            Check(x.apo->Initialize(0,nullptr)==E_INVALIDARG,"Malformed initialization rejected");
            Check(x.Init(properties)==S_OK,"Initialization through SDK property store");
            Check(x.Init(properties)==APOERR_ALREADY_INITIALIZED,"Duplicate initialization rejected");
            IAudioMediaType* supported=nullptr;
            Check(x.apo->IsInputFormatSupported(&stereo,&stereo,&supported)==S_OK&&supported==&stereo,"Float stereo negotiated via SDK vtable");if(supported)supported->Release();
            Check(x.apo->IsOutputFormatSupported(&stereo,&wrongRate,&supported)==APOERR_FORMAT_NOT_SUPPORTED,"Sample rate mismatch rejected");
            Check(x.apo->IsInputFormatSupported(nullptr,&integer,&supported)==APOERR_FORMAT_NOT_SUPPORTED,"Integer DSP buffers rejected");
            Check(x.Lock(stereo,mono)==APOERR_FORMAT_NOT_SUPPORTED,"Channel count mismatch rejected");
            Format backStereo;backStereo.data.dwChannelMask=0x30;
            Check(x.Lock(backStereo,backStereo)==APOERR_FORMAT_NOT_SUPPORTED,"F07 layout missing front left/right rejected");
            Format zeroStereo;zeroStereo.data.dwChannelMask=0;
            Check(x.apo->IsInputFormatSupported(&stereo,&zeroStereo,&supported)==S_OK,"F07 zero-mask stereo explicitly means front left/right");if(supported)supported->Release();
            Format zeroMulti(6);zeroMulti.data.dwChannelMask=0;
            Check(x.apo->IsInputFormatSupported(nullptr,&zeroMulti,&supported)==APOERR_FORMAT_NOT_SUPPORTED,"F07 zero-mask multichannel layout is ambiguous and rejected");
            Format sideSurround(6);sideSurround.data.dwChannelMask=0x60f;
            Check(x.Lock(surround,sideSurround)==APOERR_FORMAT_NOT_SUPPORTED,"F07 equal channel counts with different positions rejected");
            Format badMask(6);badMask.data.dwChannelMask=3;
            Check(x.Lock(badMask,badMask)==APOERR_FORMAT_NOT_SUPPORTED,"F07 mask bit count must match channel count");
            Format zeroMono(1);zeroMono.data.dwChannelMask=0;
            Check(x.apo->IsInputFormatSupported(&mono,&zeroMono,&supported)==S_OK,"F07 zero-mask mono explicitly passes through as center");if(supported)supported->Release();
            Check(x.Lock(stereo,stereo)==S_OK,"Matching float stereo locks");
            Check(x.Lock(stereo,stereo)==APOERR_APO_LOCKED,"Repeated lock rejected");
            UINT32 channels=0;HNSTIME latency=-1;
            Check(x.apo->GetInputChannelCount(&channels)==S_OK&&channels==2,"Input channel count ABI");
            Check(x.apo->GetLatency(&latency)==S_OK&&latency==0,"Swap adds zero algorithmic latency");
            const float original[]={1,10,2,20,-3,-30,0,0};float output[8]={};
            env.state->enabled=0;auto result=x.Process(output,original,4);
            Check(result.u32BufferFlags==BUFFER_VALID&&result.u32ValidFrameCount==4&&!memcmp(output,original,sizeof(original)),"Disabled core preserves every sample");
            env.state->enabled=1;result=x.Process(output,original,4);
            const float expected[]={10,1,20,2,-30,-3,0,0};
            Check(!memcmp(output,expected,sizeof(expected)),"L and R swap simultaneously without crossfeed or level change");
            x.Process(output,output,4);
            Check(!memcmp(output,original,sizeof(original)),"In-place double swap restores exact samples");
            env.state->enabled=0;x.Process(output,original,4);
            Check(!memcmp(output,original,sizeof(original)),"Live control flag changes next buffer without reinitializing");
            Check(env.state->loads==1&&env.state->frames==16&&env.state->swapped==8&&env.state->pid==(LONG)GetCurrentProcessId(),"Shared counters reflect actual processing");
            float untouched[]={9,9,9,9};env.state->enabled=1;result=x.Process(untouched,nullptr,2,BUFFER_SILENT);
            Check(result.u32BufferFlags==BUFFER_SILENT&&result.u32ValidFrameCount==2&&untouched[0]==9,"Silent buffers remain silent without dereferencing input");
            result=x.Process(output,original,9);
            Check(result.u32BufferFlags==BUFFER_INVALID&&result.u32ValidFrameCount==0,"Oversized buffers rejected before access");
            result=x.Process(output,nullptr,4);
            Check(result.u32BufferFlags==BUFFER_INVALID&&result.u32ValidFrameCount==0,"Null valid input cannot emit stale output");
            Check(x.rt->CalcInputFrames(123)==123&&x.rt->CalcOutputFrames(123)==123,"Frame counts and sample rate preserved");
            IAudioSystemEffects2* effects=nullptr;GUID* list=nullptr;UINT count=100;
            Check(x.apo->QueryInterface(FxId,(void**)&effects)==S_OK&&effects->GetEffectsList(&list,&count,nullptr)==S_OK&&count==0,"System effect discovery supported");if(effects)effects->Release();
            Check(x.config->UnlockForProcess()==S_OK,"Unlock succeeds");
            Check(x.config->UnlockForProcess()==APOERR_ALREADY_UNLOCKED,"Repeated unlock reports SDK error");
            result=x.Process(output,original,4);Check(result.u32BufferFlags==BUFFER_INVALID,"Processing after unlock is refused");
        }
        {
            Instance x;x.Create(factory);x.Init(properties);x.Lock(mono,mono);
            float values[]={1,2,3,4},output[4]={};env.state->enabled=1;x.Process(output,values,4);
            Check(!memcmp(values,output,sizeof(values)),"Mono passes through unchanged");
        }
        {
            Instance x;x.Create(factory);x.Init(properties);x.Lock(surround,surround);
            float values[]={1,2,3,4,5,6,7,8,9,10,11,12},output[12]={};const float expected[]={2,1,3,4,5,6,8,7,9,10,11,12};
            env.state->enabled=1;x.Process(output,values,2);
            Check(!memcmp(expected,output,sizeof(expected)),"Six channels swap only front L/R; other channels preserved");
        }
        {
            Format eight(8);Instance x;x.Create(factory);x.Init(properties);
            Check(x.Lock(eight,eight)==S_OK,"F07 eight-channel speaker layout locks");
            float values[]={1,2,3,4,5,6,7,8},output[8]={};const float expected[]={2,1,3,4,5,6,7,8};
            env.state->enabled=1;x.Process(output,values,1);
            Check(!memcmp(expected,output,sizeof(expected)),"F07 eight channels preserve center, LFE, rear and side speakers");
        }
        auto failLock=(void(WINAPI*)(LONG))GetProcAddress(library,"ChannelFlipTestFailLock");
        auto heldLocks=(LONG(WINAPI*)())GetProcAddress(library,"ChannelFlipTestHeldLocks");
        if(failLock&&heldLocks) {
            LONG required=0;
            {
                Instance x;x.Create(factory);x.Init(properties);Check(x.Lock(stereo,stereo)==S_OK,"F03 instrumented resident lock succeeds");required=heldLocks();
                Check(required>=3,"F03 image, dedicated instance and shared mapping are resident");
            }
            Check(heldLocks()==0,"F03 release unlocks all owned ranges");
            for(LONG fail=1;fail<=required;++fail) {
                Instance x;x.Create(factory);x.Init(properties);failLock(fail);
                HRESULT hr=x.Lock(stereo,stereo);
                Check(hr==HRESULT_FROM_WIN32(ERROR_WORKING_SET_QUOTA)&&env.state->error==hr,"F03 lock failure is returned and recorded");
                Check(heldLocks()==0,"F03 partial lock failure releases earlier ranges");
                failLock(0);Check(x.Lock(stereo,stereo)==S_OK&&env.state->error==0,"F03 failed lock can be retried after recovery");
            }
            {
                Instance x,y;x.Create(factory);x.Init(properties);y.Create(factory);y.Init(properties);
                x.Lock(stereo,stereo);LONG first=heldLocks();y.Lock(stereo,stereo);
                Check(heldLocks()==first+2,"F03 concurrent instances share one image residency reference");
                x.config->UnlockForProcess();Check(heldLocks()==first,"F03 unlocking one instance does not unpin the other's image");
                float values[]={1,2},output[2]={};env.state->enabled=1;y.Process(output,values,1);
                Check(output[0]==2&&output[1]==1,"F03 remaining instance still processes after peer unlock");
            }
            Check(heldLocks()==0,"F03 final shared image reference releases all ranges");
        }
        if(argc==4) {
            Instance x;x.Create(factory);x.Init(properties);env.state->enabled=1;
            if(x.Lock(stereo,stereo,480)==S_OK)RunStress(x,argv[3]);else Check(false,"F03 pressure test process lock");
        }
        {
            env.state->magic=0;Instance x;x.Create(factory);Check(x.Init(properties)==S_OK,"Damaged state file degrades to pass-through");x.Lock(stereo,stereo);
            float values[]={1,2,3,4},output[4]={};x.Process(output,values,2);
            Check(!memcmp(values,output,sizeof(values)),"Damaged control data never corrupts audio");env.state->magic=0x50464c43;
        }
        {
            env.Child(L"{abababab-abab-abab-abab-abababababab}");Instance x;x.Create(factory);
            HRESULT result=x.Init(properties);Check(FAILED(result)&&env.state->error==result,"Unavailable original effect fails explicitly and records error");env.Child(L"");
            Check(x.Init(properties)==S_OK,"Retry after failed child initialization recovers cleanly");
        }
        {
            ChildFactory original;DWORD cookie=0;
            HRESULT registered=CoRegisterClassObject(ChildId,&original,CLSCTX_INPROC_SERVER,REGCLS_MULTIPLEUSE,&cookie);
            Check(registered==S_OK,"Original-effect test double registered only inside test process");
            if(SUCCEEDED(registered)) {
                env.Child(L"{abababab-abab-abab-abab-abababababab}");
                {
                    Instance x;x.Create(factory);
                    HRESULT initialized=x.Init(properties);Check(initialized==S_OK&&original.child.initializations==1,"Original effect initialized with its own class ID");
                    if(SUCCEEDED(initialized)) {
                        Check(x.Lock(stereo,stereo)==S_OK&&original.child.locks==1,"Original effect receives process lock");
                        float values[]={2,20,4,40},output[4]={};env.state->enabled=1;x.Process(output,values,2);
                        const float swapped[]={10,1,20,2};
                        Check(!memcmp(output,swapped,sizeof(output))&&original.child.processes==1,"Original DSP runs before stereo swap");
                        env.state->enabled=0;x.Process(output,values,2);const float unchanged[]={1,10,2,20};
                        Check(!memcmp(output,unchanged,sizeof(output)),"Turning swap off keeps original DSP active");
                        HNSTIME latency=0;Check(x.apo->GetLatency(&latency)==S_OK&&latency==17,"Original effect latency preserved");
                        IAudioSystemEffects2* effects=nullptr;GUID* list=nullptr;UINT count=0;x.apo->QueryInterface(FxId,(void**)&effects);
                        Check(effects->GetEffectsList(&list,&count,nullptr)==S_OK&&count==1&&Same(list[0],ChildId),"Original effect discovery forwarded");CoTaskMemFree(list);effects->Release();
                    }
                }
                Check(original.child.unlocks==1,"Original effect unlocked on release");
                env.Child(L"");CoRevokeClassObject(cookie);
            }
        }
    }
    factory->Release();Check(unload()==S_OK,"All COM references released; DLL unloads cleanly");
    FreeLibrary(library);CoUninitialize();printf("RESULT: %d passed; %d failed\n",passed,failed);return failed?1:0;
}
