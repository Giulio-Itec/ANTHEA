; Shared by the real installer and the isolated installation checks.
!ifndef ANTHEA_INSTALL_SAFETY
!define ANTHEA_INSTALL_SAFETY
!include LogicLib.nsh
!include FileFunc.nsh
!if ${NSIS_PTR_SIZE} != 4
  !error "InstallSafety expects the x86 NSIS stub; review PROCESSENTRY32W before changing the installer architecture."
!endif

Var ValidatedInstallDir
Var InstallSafetyError

; The NSIS executable is x86, including on x64 Windows: PROCESSENTRY32W is
; 556 bytes, with szExeFile at offset 36. No external process plug-in is needed.
!macro CHECK_NOT_RUNNING UN
Function ${UN}CheckNotRunning
  System::Store "S"
  StrCpy $6 ""
  System::Call 'kernel32::CreateToolhelp32Snapshot(i 2, i 0) p .r0'
  ${If} $0 == -1
    StrCpy $6 "Impossibile verificare i programmi in esecuzione."
  ${Else}
    System::Alloc 556
    Pop $1
    ${If} $1 == 0
      StrCpy $6 "Impossibile verificare i programmi in esecuzione."
    ${Else}
      System::Call '*$1(i 556)'
      System::Call 'kernel32::Process32FirstW(p r0, p r1) i .r2 ?e'
      Pop $7
      ${DoWhile} $2 != 0
        IntOp $3 $1 + 36
        System::Call '*$3(&w260 .r4)'
        ${If} $4 == "${EXE}"
          StrCpy $6 "${APP} è in esecuzione. Operazione interrotta: chiudere tutte le finestre di ${APP} e riavviare il setup."
          ${Break}
        ${EndIf}
        System::Call 'kernel32::Process32NextW(p r0, p r1) i .r2 ?e'
        Pop $7
      ${Loop}
      ${If} $6 == ""
      ${AndIf} $7 != 18 ; ERROR_NO_MORE_FILES is the only successful end of enumeration.
        StrCpy $6 "Impossibile completare il controllo dei programmi in esecuzione (errore $7)."
      ${EndIf}
      System::Free $1
    ${EndIf}
    System::Call 'kernel32::CloseHandle(p r0)'
  ${EndIf}
  ${If} $6 == ""
  ${AndIf} ${FileExists} "$INSTDIR\${EXE}"
    ; Also catch an executable locked by another program or started under a different name.
    ClearErrors
    FileOpen $0 "$INSTDIR\${EXE}" a
    ${If} ${Errors}
      StrCpy $6 "Il programma in $INSTDIR è in uso o non è accessibile. Chiuderlo e verificare i permessi prima di riprovare."
    ${Else}
      FileClose $0
    ${EndIf}
  ${EndIf}
  ${If} $6 != ""
    MessageBox MB_OK|MB_ICONSTOP "$6" /SD IDOK
    SetErrorLevel 10
    Abort "$6"
  ${EndIf}
  System::Store "L"
FunctionEnd
!macroend

; Inspect the entire existing tree before removing anything. In particular, never
; follow a directory junction or a symbolic link during recursive cleanup.
Function CheckInstallTree
  Exch $0
  Push $1
  Push $2
  Push $3
  ClearErrors
  FindFirst $1 $2 "$0\*"
  ${If} ${Errors}
    StrCpy $InstallSafetyError "Impossibile leggere la cartella $0."
  ${Else}
    ${Do}
      ${If} $2 != "."
      ${AndIf} $2 != ".."
        System::Call 'kernel32::GetFileAttributesW(w "$0\$2") i .r3'
        ${If} $3 == -1
          StrCpy $InstallSafetyError "Impossibile controllare $0\$2."
        ${Else}
          IntOp $3 $3 & 0x410 ; FILE_ATTRIBUTE_REPARSE_POINT | DIRECTORY
          ${If} $3 >= 0x400
            StrCpy $InstallSafetyError "La cartella contiene un collegamento: $0\$2. Rimuovere il collegamento prima di installare."
          ${ElseIf} $3 == 16
            Push "$0\$2"
            Call CheckInstallTree
          ${EndIf}
        ${EndIf}
      ${EndIf}
      ${If} $InstallSafetyError != ""
        ${Break}
      ${EndIf}
      ClearErrors
      FindNext $1 $2
      ${If} ${Errors}
        ${Break}
      ${EndIf}
    ${Loop}
    FindClose $1
  ${EndIf}
  Pop $3
  Pop $2
  Pop $1
  Pop $0
FunctionEnd

; Canonical local absolute path only. Guard the target and ALL its ancestors,
; then inspect its contents. This runs both before the old uninstaller and again
; immediately before cleanup, since the old uninstaller can alter the directory.
Function ValidateInstallDirectory
  System::Store "S"
  StrCpy $InstallSafetyError ""
  StrCpy $1 $INSTDIR 2 1
  ${If} $1 != ":\"
    StrCpy $InstallSafetyError "Scegliere una cartella locale con percorso assoluto."
    Goto invalid
  ${EndIf}
  System::Call 'kernel32::GetFullPathNameW(w "$INSTDIR", i ${NSIS_MAX_STRLEN}, w .r0, p 0) i .r1'
  ${If} $1 == 0
  ${OrIf} $1 >= ${NSIS_MAX_STRLEN}
    StrCpy $InstallSafetyError "Il percorso di installazione non è valido o è troppo lungo."
    Goto invalid
  ${EndIf}
  StrCpy $1 $0 1 -1
  ${If} $1 == "\"
    StrCpy $0 $0 -1
  ${EndIf}
  StrLen $1 $0
  ${If} $1 <= 3
    StrCpy $InstallSafetyError "Non è possibile installare nella radice del disco."
    Goto invalid
  ${EndIf}
  StrCpy $INSTDIR $0

  ; Never target a system/profile/shared root, or an ancestor containing one.
  !macro ProtectInstallRoot PATH
    StrCpy $2 "${PATH}\"
    StrCpy $3 "$INSTDIR\"
    StrLen $4 $3
    StrCpy $2 $2 $4
    ${If} $2 == $3
      StrCpy $InstallSafetyError "La destinazione contiene una cartella di sistema o dell'utente: ${PATH}."
      Goto invalid
    ${EndIf}
  !macroend
  !insertmacro ProtectInstallRoot "$WINDIR"
  !insertmacro ProtectInstallRoot "$PROGRAMFILES64"
  !insertmacro ProtectInstallRoot "$PROGRAMFILES32"
  !insertmacro ProtectInstallRoot "$COMMONFILES64"
  !insertmacro ProtectInstallRoot "$PROFILE"
  !insertmacro ProtectInstallRoot "$LOCALAPPDATA"
  !insertmacro ProtectInstallRoot "$APPDATA"
  !insertmacro ProtectInstallRoot "$DOCUMENTS"
  !insertmacro ProtectInstallRoot "$DESKTOP"
  !insertmacro ProtectInstallRoot "$EXEPATH"
  StrCpy $2 "$WINDIR\"
  StrLen $3 $2
  StrCpy $4 "$INSTDIR\" $3
  ${If} $4 == $2
    StrCpy $InstallSafetyError "La destinazione non può essere dentro Windows."
    Goto invalid
  ${EndIf}

  StrCpy $0 $INSTDIR
  ${Do}
    System::Call 'kernel32::GetFileAttributesW(w r0) i .r1 ?e'
    Pop $2
    ${If} $1 == -1
      ${If} $2 != 2
      ${AndIf} $2 != 3
        StrCpy $InstallSafetyError "Impossibile verificare il percorso $0 (errore $2)."
        Goto invalid
      ${EndIf}
    ${Else}
      IntOp $1 $1 & 0x410
      ${If} $1 != 16
        StrCpy $InstallSafetyError "La destinazione o un suo percorso padre è un collegamento o non è una cartella: $0."
        Goto invalid
      ${EndIf}
    ${EndIf}
    ${GetParent} "$0" $1
    ${If} $1 == ""
    ${OrIf} $1 == $0
      ${Break}
    ${EndIf}
    StrCpy $0 $1
  ${Loop}

  ${If} ${FileExists} "$INSTDIR\*"
    ; Old custom installation paths remain supported. An arbitrary populated
    ; directory must not be erased merely because it was entered in /D=.
    ${GetFileName} "$INSTDIR" $0
    ${If} $0 != "${APP}"
    ${AndIf} $ValidatedInstallDir != $INSTDIR
      ${IfNot} ${FileExists} "$INSTDIR\${EXE}"
      ${OrIfNot} ${FileExists} "$INSTDIR\ANTHEA.runtimeconfig.json"
        StrCpy $InstallSafetyError "Usare una cartella dedicata di nome ANTHEA oppure la cartella di una precedente installazione di ANTHEA."
        Goto invalid
      ${EndIf}
    ${EndIf}
    Push "$INSTDIR"
    Call CheckInstallTree
  ${EndIf}
  invalid:
  ${If} $InstallSafetyError != ""
    MessageBox MB_OK|MB_ICONSTOP "$InstallSafetyError$\r$\nInstallazione interrotta prima della copia dei nuovi file." /SD IDOK
    SetErrorLevel 11
    Abort "$InstallSafetyError"
  ${EndIf}
  StrCpy $ValidatedInstallDir $INSTDIR
  System::Store "L"
FunctionEnd

Function CleanInstallDirectory
  Call CheckNotRunning
  Call ValidateInstallDirectory
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  DetailPrint "Pulizia completa della cartella $INSTDIR"
  ${If} ${FileExists} "$INSTDIR\*"
    ClearErrors
    RMDir /r "$INSTDIR"
    ${If} ${Errors}
    ${OrIf} ${FileExists} "$INSTDIR\*"
      MessageBox MB_OK|MB_ICONSTOP "Impossibile svuotare $INSTDIR. Chiudere i programmi che usano questi file e riprovare. I nuovi file non sono stati copiati." /SD IDOK
      SetErrorLevel 12
      Abort "Pulizia della cartella incompleta: installazione interrotta."
    ${EndIf}
  ${EndIf}
FunctionEnd
!endif
