; Installer di ANTHEA (NSIS 3).
; Compilare con Crea-Installer.ps1, che pubblica l'applicazione self-contained win-x64,
; prepara guide e immagini e genera gli elenchi dei file da installare e da rimuovere.

Unicode true
ManifestDPIAware true
SetCompressor /SOLID lzma
SetCompressorDictSize 64

!ifndef VERSION
  !error "Definire VERSION (es. /DVERSION=1.0.0): usare Crea-Installer.ps1"
!endif
!ifndef VERSION4
  !error "Definire VERSION4 (es. /DVERSION4=1.0.0.0)"
!endif
!ifndef STAGE
  !error "Definire STAGE, la cartella preparata da Crea-Installer.ps1"
!endif
!ifndef OUTFILE
  !error "Definire OUTFILE, il percorso del setup"
!endif
!ifndef ICON
  !error "Definire ICON, l'icona dell'applicazione"
!endif
!ifndef GUIDE_REV
  !define GUIDE_REV "?"
!endif

!define APP "ANTHEA"
!define COMPANY "ITEC Engineering"
!define EXE "ANTHEA.exe"
!define PROGID "ANTHEA.Archivio"
!define APPKEY "Software\ITEC\ANTHEA"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ANTHEA"

Name "${APP}"
OutFile "${OUTFILE}"
BrandingText "${APP} ${VERSION}"
ShowInstDetails show
ShowUninstDetails show

; Installazione per tutti gli utenti (Programmi, richiede amministratore) oppure per
; l'utente corrente (%LOCALAPPDATA%\Programs, senza diritti). Da riga di comando:
; /AllUsers o /CurrentUser, /S silenzioso, /D=cartella (ultimo argomento).
!define MULTIUSER_EXECUTIONLEVEL Highest
!define MULTIUSER_MUI
!define MULTIUSER_INSTALLMODE_COMMANDLINE
!define MULTIUSER_USE_PROGRAMFILES64
!define MULTIUSER_INSTALLMODE_INSTDIR "${APP}"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_KEY "${APPKEY}"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_VALUENAME "InstallDir"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_KEY "${APPKEY}"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_VALUENAME "InstallMode"
!define MULTIUSER_INSTALLMODE_FUNCTION KeepCommandLineDir
!define MULTIUSER_INSTALLMODEPAGE_SHOWUSERNAME
!define MULTIUSER_INIT_TEXT_ADMINREQUIRED "L'installazione di ${APP} richiede i diritti di amministratore."
!define MULTIUSER_INIT_TEXT_POWERREQUIRED "L'installazione di ${APP} richiede i diritti di amministratore."
!define MULTIUSER_INIT_TEXT_ALLUSERSNOTPOSSIBLE "Il tuo account non ha i diritti per installare ${APP} per tutti gli utenti di questo computer."

!include MultiUser.nsh
!include MUI2.nsh
!include LogicLib.nsh
!include x64.nsh
!include WinVer.nsh
!include FileFunc.nsh

Var CommandLineDir

!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_ABORTWARNING
!define MUI_WELCOMEFINISHPAGE_BITMAP "${STAGE}\welcome.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "${STAGE}\header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "${STAGE}\header.bmp"
!define MUI_COMPONENTSPAGE_SMALLDESC

!define MUI_WELCOMEPAGE_TITLE "Installazione di ${APP} ${VERSION}"
!define MUI_WELCOMEPAGE_TEXT "${APP} è l'applicazione ${COMPANY} per il calcolo strutturale e geotecnico.$\r$\n$\r$\nIl runtime .NET 8 è incluso: non servono altri componenti.$\r$\n$\r$\nSe ${APP} è aperto, chiudilo prima di proseguire.$\r$\n$\r$\n$(^ClickNext)"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MULTIUSER_PAGE_INSTALLMODE
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Avvia ${APP}"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApplication
!define MUI_FINISHPAGE_NOREBOOTSUPPORT
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Italian"

VIProductVersion "${VERSION4}"
VIFileVersion "${VERSION4}"
VIAddVersionKey /LANG=${LANG_ITALIAN} "ProductName" "${APP}"
VIAddVersionKey /LANG=${LANG_ITALIAN} "CompanyName" "${COMPANY}"
VIAddVersionKey /LANG=${LANG_ITALIAN} "FileDescription" "Installazione di ${APP}"
VIAddVersionKey /LANG=${LANG_ITALIAN} "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=${LANG_ITALIAN} "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=${LANG_ITALIAN} "LegalCopyright" "© 2026 ${COMPANY}"

; A running ANTHEA.exe cannot be opened for writing: ask to close it before touching the files.
!macro CHECK_NOT_RUNNING UN
Function ${UN}CheckNotRunning
  ${If} ${FileExists} "$INSTDIR\${EXE}"
    ${Do}
      ClearErrors
      FileOpen $0 "$INSTDIR\${EXE}" a
      ${IfNot} ${Errors}
        FileClose $0
        ${Break}
      ${EndIf}
      MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "${APP} è in esecuzione. Chiudilo e premi Riprova." /SD IDCANCEL IDRETRY retry
      Abort "${APP} è in esecuzione: operazione annullata."
      retry:
    ${Loop}
  ${EndIf}
FunctionEnd
!macroend
!insertmacro CHECK_NOT_RUNNING ""
!insertmacro CHECK_NOT_RUNNING "un."

; /D= from the command line wins over the default folder of the install mode.
Function KeepCommandLineDir
  ${If} $CommandLineDir != ""
    StrCpy $INSTDIR $CommandLineDir
  ${EndIf}
FunctionEnd

; The installer may run elevated: start ANTHEA through Explorer as the logged-on user.
Function LaunchApplication
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\${EXE}"'
FunctionEnd

; The previous version is removed with its own uninstaller, which knows its file list.
Function RemovePreviousVersion
  ReadRegStr $1 SHCTX "${UNINSTKEY}" "InstallLocation"
  ${If} $1 == ""
    StrCpy $1 $INSTDIR
  ${EndIf}
  ${If} ${FileExists} "$1\Uninstall.exe"
    DetailPrint "Rimozione della versione installata in $1"
    ExecWait '"$1\Uninstall.exe" /S /$MultiUser.InstallMode _?=$1' $0
    ${If} $0 != 0
      Abort "Non è stato possibile rimuovere la versione precedente (codice $0)."
    ${EndIf}
    Delete "$1\Uninstall.exe"
    RMDir "$1"
  ${EndIf}
FunctionEnd

!macro ASSOCIATE EXT
  ReadRegStr $0 SHCTX "Software\Classes\${EXT}" ""
  ${If} $0 != ""
  ${AndIf} $0 != "${PROGID}"
    WriteRegStr SHCTX "Software\Classes\${EXT}" "ANTHEA.Precedente" $0
  ${EndIf}
  WriteRegStr SHCTX "Software\Classes\${EXT}" "" "${PROGID}"
  WriteRegStr SHCTX "Software\Classes\${EXT}\OpenWithProgids" "${PROGID}" ""
!macroend

!macro UNASSOCIATE EXT
  ReadRegStr $0 SHCTX "Software\Classes\${EXT}" ""
  ${If} $0 == "${PROGID}"
    ReadRegStr $1 SHCTX "Software\Classes\${EXT}" "ANTHEA.Precedente"
    ${If} $1 != ""
      WriteRegStr SHCTX "Software\Classes\${EXT}" "" $1
    ${Else}
      DeleteRegValue SHCTX "Software\Classes\${EXT}" ""
    ${EndIf}
  ${EndIf}
  DeleteRegValue SHCTX "Software\Classes\${EXT}" "ANTHEA.Precedente"
  DeleteRegValue SHCTX "Software\Classes\${EXT}\OpenWithProgids" "${PROGID}"
  DeleteRegKey /ifempty SHCTX "Software\Classes\${EXT}\OpenWithProgids"
  DeleteRegKey /ifempty SHCTX "Software\Classes\${EXT}"
!macroend

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "${APP} richiede Windows a 64 bit." /SD IDOK
    Quit
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP "${APP} richiede Windows 10 o successivo." /SD IDOK
    Quit
  ${EndIf}
  SetRegView 64
  StrCpy $CommandLineDir $INSTDIR
  !insertmacro MULTIUSER_INIT
FunctionEnd

Function un.onInit
  SetRegView 64
  !insertmacro MULTIUSER_UNINIT
FunctionEnd

Section "${APP}" SecApp
  SectionIn RO
  Call CheckNotRunning
  Call RemovePreviousVersion
  ; Uninstaller first: even an incomplete installation can be removed from Apps & Features.
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr SHCTX "${APPKEY}" "InstallDir" "$INSTDIR"
  WriteRegStr SHCTX "${APPKEY}" "InstallMode" "$MultiUser.InstallMode"
  WriteRegStr SHCTX "${APPKEY}" "Version" "${VERSION}"
  WriteRegStr SHCTX "${UNINSTKEY}" "DisplayName" "${APP}"
  WriteRegStr SHCTX "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr SHCTX "${UNINSTKEY}" "Publisher" "${COMPANY}"
  WriteRegStr SHCTX "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\${EXE},0"
  WriteRegStr SHCTX "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr SHCTX "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe" /$MultiUser.InstallMode'
  WriteRegStr SHCTX "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /$MultiUser.InstallMode /S'
  WriteRegDWORD SHCTX "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD SHCTX "${UNINSTKEY}" "NoRepair" 1

  ; A file that cannot be written is "ignored" by NSIS (always in silent mode) and only sets
  ; the error flag: stop here instead of reporting a successful installation.
  ClearErrors
  !include "${STAGE}\install-files.nsh"
  ${If} ${Errors}
    Abort "Alcuni file non sono stati scritti in $INSTDIR: installazione incompleta."
  ${EndIf}
  SetOutPath "$INSTDIR"

  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\${APP}.lnk" "$INSTDIR\${EXE}"
SectionEnd

Section "Documentazione e guide in PDF" SecGuides
  SectionIn RO
  ClearErrors
  !include "${STAGE}\install-guides.nsh"
  ${If} ${Errors}
    Abort "Le guide non sono state scritte in $INSTDIR\Guide: installazione incompleta."
  ${EndIf}
  !include "${STAGE}\guide-shortcuts.nsh"
  SetOutPath "$INSTDIR"
SectionEnd

Section "Collegamento sul desktop" SecDesktop
  CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\${EXE}"
SectionEnd

Section "Apri i file .anthea e .programma con ${APP}" SecAssociations
  WriteRegStr SHCTX "Software\Classes\${PROGID}" "" "Archivio ${APP}"
  WriteRegStr SHCTX "Software\Classes\${PROGID}\DefaultIcon" "" "$INSTDIR\${EXE},0"
  WriteRegStr SHCTX "Software\Classes\${PROGID}\shell\open\command" "" '"$INSTDIR\${EXE}" "%1"'
  !insertmacro ASSOCIATE ".anthea"
  !insertmacro ASSOCIATE ".programma"
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
SectionEnd

Section "-EstimatedSize"
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD SHCTX "${UNINSTKEY}" "EstimatedSize" $0
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecApp} "Programma ${APP} ${VERSION} con il runtime .NET 8 incluso."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecGuides} "Documentazione inclusa: uso e teoria di tutti i moduli documentati (Rev${GUIDE_REV}), approfondimenti e indice. Consultabile offline dal menu Start."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Collegamento ad ${APP} sul desktop."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAssociations} "Apre gli archivi .anthea e .programma con un doppio clic."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  Call un.CheckNotRunning
  !include "${STAGE}\uninstall-files.nsh"
  !include "${STAGE}\uninstall-guides.nsh"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"

  Delete "$SMPROGRAMS\${APP}\${APP}.lnk"
  !include "${STAGE}\uninstall-guide-shortcuts.nsh"
  RMDir "$SMPROGRAMS\${APP}"
  Delete "$DESKTOP\${APP}.lnk"

  !insertmacro UNASSOCIATE ".anthea"
  !insertmacro UNASSOCIATE ".programma"
  DeleteRegKey SHCTX "Software\Classes\${PROGID}"
  DeleteRegKey SHCTX "${UNINSTKEY}"
  DeleteRegKey SHCTX "${APPKEY}"
  DeleteRegKey /ifempty SHCTX "Software\ITEC"
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
SectionEnd
