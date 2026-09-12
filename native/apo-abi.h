#pragma once
// Windows APO ABI declarations, matched against Microsoft's audioenginebaseapo.h
// and audiomediatype.h. No third-party DSP implementation is used.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <unknwn.h>
#include <propsys.h>
#include <mmreg.h>
#include <audioapotypes.h>

inline constexpr GUID CF_CLSID = {0xf81b4c35,0x7458,0x4c1e,{0xb8,0x20,0x77,0x0a,0x91,0x4e,0xd4,0x36}};
inline constexpr GUID CF_IID_APO = {0xfd7f2b29,0x24d0,0x4b5c,{0xb1,0x77,0x59,0x2c,0x39,0xf9,0xca,0x10}};
inline constexpr GUID CF_IID_RT = {0x9e1d6a6d,0xddbc,0x4e95,{0xa4,0xc7,0xad,0x64,0xba,0x37,0x84,0x6c}};
inline constexpr GUID CF_IID_CFG = {0x0e5ed805,0xaba6,0x49c3,{0x8f,0x9a,0x2b,0x8c,0x88,0x9c,0x4f,0xa8}};
inline constexpr GUID CF_IID_FX = {0x5fa00f27,0xadd6,0x499a,{0x8a,0x9d,0x6b,0x98,0x52,0x1f,0xa7,0x5b}};
inline constexpr GUID CF_IID_FX2 = {0xbafe99d2,0x7436,0x44ce,{0x9e,0x0e,0x4d,0x89,0xaf,0xbf,0xff,0x56}};
inline constexpr GUID CF_FLOAT = {3,0,0x10,{0x80,0,0,0xaa,0,0x38,0x9b,0x71}};
inline constexpr PROPERTYKEY CF_ENDPOINT_GUID = {{0x1da5d803,0xd492,0x4edd,{0x8c,0x23,0xe0,0xc0,0xff,0xee,0x7f,0x0e}},4};

struct CF_FORMAT { GUID type; DWORD channels; DWORD bytes; DWORD validBits; FLOAT rate; DWORD mask; };
struct CFMediaType : IUnknown {
    virtual HRESULT STDMETHODCALLTYPE IsCompressedFormat(BOOL*) = 0;
    virtual HRESULT STDMETHODCALLTYPE IsEqual(CFMediaType*,DWORD*) = 0;
    virtual const WAVEFORMATEX* STDMETHODCALLTYPE GetAudioFormat() = 0;
    virtual HRESULT STDMETHODCALLTYPE GetUncompressedAudioFormat(CF_FORMAT*) = 0;
};
struct CF_DESCRIPTOR { int type; UINT_PTR buffer; UINT32 maxFrames; CFMediaType* format; UINT32 signature; };
struct CF_REG_PROPERTIES {
    CLSID clsid; UINT32 flags; WCHAR name[256]; WCHAR copyright[256];
    UINT32 major,minor,minInputs,maxInputs,minOutputs,maxOutputs,maxInstances,numInterfaces;
    IID interfaces[1];
};
struct CF_INIT_BASE { UINT32 size; CLSID clsid; };
struct CF_INIT {
    CF_INIT_BASE base; IPropertyStore* endpoint; IPropertyStore* effects;
    void* reserved; IUnknown* devices;
};
struct CFApo : IUnknown {
    virtual HRESULT STDMETHODCALLTYPE Reset() = 0;
    virtual HRESULT STDMETHODCALLTYPE GetLatency(HNSTIME*) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetRegistrationProperties(CF_REG_PROPERTIES**) = 0;
    virtual HRESULT STDMETHODCALLTYPE Initialize(UINT32,BYTE*) = 0;
    virtual HRESULT STDMETHODCALLTYPE IsInputFormatSupported(CFMediaType*,CFMediaType*,CFMediaType**) = 0;
    virtual HRESULT STDMETHODCALLTYPE IsOutputFormatSupported(CFMediaType*,CFMediaType*,CFMediaType**) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetInputChannelCount(UINT32*) = 0;
};
struct CFApoConfig : IUnknown {
    virtual HRESULT STDMETHODCALLTYPE LockForProcess(UINT32,CF_DESCRIPTOR**,UINT32,CF_DESCRIPTOR**) = 0;
    virtual HRESULT STDMETHODCALLTYPE UnlockForProcess() = 0;
};
struct CFApoRT : IUnknown {
    virtual void STDMETHODCALLTYPE APOProcess(UINT32,APO_CONNECTION_PROPERTY**,UINT32,APO_CONNECTION_PROPERTY**) = 0;
    virtual UINT32 STDMETHODCALLTYPE CalcInputFrames(UINT32) = 0;
    virtual UINT32 STDMETHODCALLTYPE CalcOutputFrames(UINT32) = 0;
};
struct CFApoEffects : IUnknown {
    virtual HRESULT STDMETHODCALLTYPE GetEffectsList(GUID**,UINT*,HANDLE) = 0;
};

struct CF_STATE {
    DWORD magic; DWORD version;
    volatile LONG enabled;
    volatile LONG rate;
    volatile LONG channels;
    volatile LONG loads;
    volatile LONG64 frames;
    volatile LONG64 swappedFrames;
    volatile LONG error;
    volatile LONG lastProcess;
};
static_assert(sizeof(CF_DESCRIPTOR) == 40, "APO descriptor ABI");
static_assert(sizeof(CF_INIT) == 56, "APO init ABI");
static_assert(sizeof(CF_FORMAT) == 36, "APO format ABI");
static_assert(sizeof(CF_STATE) == 48, "state ABI");
inline constexpr DWORD CF_MAGIC = 0x50464c43;
