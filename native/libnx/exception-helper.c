// MonoMod ARM64 exception-helper embedding adapter.
// Link the existing exhelper_linux_macos_arm64.S ELF implementation with real
// pthread TLS and DWARF unwinding. No shared-library extraction is required.
#include <stddef.h>
extern void eh_get_exception_ptr(void);
extern void eh_managed_to_native(void);
extern void eh_native_to_managed(void);
void* monomod_libnx_exception_helper(int index)
{
    switch (index) {
    case 0: return (void*)eh_get_exception_ptr;
    case 1: return (void*)eh_managed_to_native;
    case 2: return (void*)eh_native_to_managed;
    default: return NULL;
    }
}
