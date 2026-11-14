// Stand-ins for four native functions that the SLRGTk 0.2.0 iOS plugin (slrgtk_agnostic.framework) does not contain,
// but that the toolkit's generated C# code (_Natives_slrgtk_agnostic.cs) links against on iOS ("__Internal").
// They let the iPhone build link. They do nothing except print a line the first few times they are called,
// so you can see in Xcode's console whether the toolkit ever really needs them.
//
// TEMPORARY: delete this file as soon as the toolkit team ships an iOS plugin that includes the real functions.
// (If both exist, the build fails with "duplicate symbol", which is the signal to delete this file.)

#include <stdint.h>
#include <stdio.h>

static int g_stubCalls = 0;
static void Note(const char* name)
{
    if (g_stubCalls < 10)
    {
        g_stubCalls++;
        fprintf(stderr, "[SLRGTK STUB] %s was called. This is a stand-in that does nothing. If signing does not work, tell Claude about this line.\n", name);
    }
}

// int NativeHost(ref ImplementationHost host)
int agnostic_cs_host(void* host)
{
    (void)host;
    Note("agnostic_cs_host");
    return 0;
}

// void NativeReply(IntPtr reply, int code, byte[] payload, int length)
void agnostic_cs_reply(void* reply, int code, const uint8_t* payload, int length)
{
    (void)reply; (void)code; (void)payload; (void)length;
    Note("agnostic_cs_reply");
}

// int NativeEmit(ulong route, uint evt, byte[] payload, int length)
int agnostic_cs_emit(uint64_t route, uint32_t evt, const uint8_t* payload, int length)
{
    (void)route; (void)evt; (void)payload; (void)length;
    Note("agnostic_cs_emit");
    return 0;
}

// IntPtr NativeLend(string service, IntPtr obj, out IntPtr resBytes, out int resLen)
void* agnostic_cs_lend(const char* service, void* obj, void** resBytes, int* resLen)
{
    (void)service; (void)obj;
    if (resBytes) *resBytes = 0;
    if (resLen) *resLen = 0;
    Note("agnostic_cs_lend");
    return 0;
}
