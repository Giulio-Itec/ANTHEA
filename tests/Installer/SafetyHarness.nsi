; No registry, associations, real application files or shell shortcuts.
Unicode true
RequestExecutionLevel user
Name "ANTHEA installation safety test"
OutFile "${SETUP}"
SilentInstall silent
!define APP "ANTHEA"
!define EXE "ANTHEA.exe"
!include "${SAFETY}"
!insertmacro CHECK_NOT_RUNNING ""

Function .onInit
  Call CheckNotRunning
FunctionEnd

Function .onInstFailed
  FileOpen $0 "${SETUP}.failure.txt" w
  FileWrite $0 "$InstallSafetyError"
  FileClose $0
FunctionEnd

Section
  Call CheckNotRunning
  Call ValidateInstallDirectory
!ifndef VALIDATE_ONLY
  ; Absolute containment verified before invoking any cleanup in this harness.
  GetFullPathName $0 "${TESTROOT}\"
  StrLen $1 $0
  StrCpy $2 "$INSTDIR\" $1
  ${If} $2 != $0
    SetErrorLevel 88
    Abort "Outside the isolated fixture"
  ${EndIf}
  ; Represents the previous uninstaller, so tests prove the process/path guards
  ; happen BEFORE any action that could remove the previous application.
  FileOpen $0 "${TRACE}" w
  FileWrite $0 "Previous uninstaller called"
  FileClose $0
  Call CleanInstallDirectory
  SetOutPath "$INSTDIR"
  FileOpen $0 "$INSTDIR\new-payload.txt" w
  FileWrite $0 "1.8.0"
  FileClose $0
!endif
SectionEnd
