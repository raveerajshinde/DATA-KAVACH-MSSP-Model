/* EnterpriseDPDP_Setup.exe - Data Kavach installer bootstrapper.
   Runs install.ps1 (next to this exe) elevated with Windows PowerShell and forwards command-line arguments. */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <wchar.h>
#include <shellapi.h>

int WINAPI wWinMain(HINSTANCE h, HINSTANCE p, PWSTR cmd, int show) {
    wchar_t exe[MAX_PATH], dir[MAX_PATH], script[MAX_PATH], args[8192], ps[MAX_PATH];
    GetModuleFileNameW(NULL, exe, MAX_PATH);
    wcscpy(dir, exe);
    wchar_t *slash = wcsrchr(dir, L'\\');
    if (slash) *slash = 0;
    swprintf(script, MAX_PATH, L"%ls\\install.ps1", dir);
    if (GetFileAttributesW(script) == INVALID_FILE_ATTRIBUTES) {
        MessageBoxW(NULL, L"install.ps1 was not found next to EnterpriseDPDP_Setup.exe.\nExtract the complete Data Kavach package and run the setup from that folder.", L"Data Kavach Setup", MB_ICONERROR);
        return 1;
    }
    GetSystemDirectoryW(ps, MAX_PATH);
    wcscat(ps, L"\\WindowsPowerShell\\v1.0\\powershell.exe");
    swprintf(args, 8192, L"-NoProfile -ExecutionPolicy Bypass -NoExit -File \"%ls\" %ls", script, cmd ? cmd : L"");
    SHELLEXECUTEINFOW sei = { sizeof(sei) };
    sei.fMask = SEE_MASK_NOCLOSEPROCESS;
    sei.lpVerb = L"runas";
    sei.lpFile = ps;
    sei.lpParameters = args;
    sei.lpDirectory = dir;
    sei.nShow = SW_SHOWNORMAL;
    if (!ShellExecuteExW(&sei)) {
        if (GetLastError() == ERROR_CANCELLED) MessageBoxW(NULL, L"Administrator approval is required to install Data Kavach.", L"Data Kavach Setup", MB_ICONWARNING);
        return 1;
    }
    return 0;
}
