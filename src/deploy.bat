CLS
ECHO OFF

SET /P "major=Please Enter Major Number: "
SET /P "minor=Please Enter Minor Number: "
SET /P "build=Please Enter Build Number: "
SET "version=%major%.%minor%.%build%"

SET "vDir=MultiAlign-v"
SET "finalVersion=%vDir%%version%"
SET "outpath=\\floyd\software\MultiAlign\test\%finalVersion%"
SET "localpath=m:\software\proteomics\MultiAlign\test\%finalVersion%"

ECHO %outpath%

mkdir %localpath%
explorer %localpath%

SET "x86=%localpath%\x86"
SET "x64=%localpath%\x64"

mkdir %x86%
mkdir %x64%

mkdir %x86%\MultiAlignParameterFileEditor
mkdir %x86%\Manassa
mkdir %x86%\MultiAlignConsole

mkdir %x64%\MultiAlignParameterFileEditor
mkdir %x64%\Manassa
mkdir %x64%\MultiAlignConsole

xcopy /S .\MultiAlignParameterFileEditor\bin\x86\release\*  	%x86%\MultiAlignParameterFileEditor
xcopy /S .\Manassa\bin\x86\release\*  				%x86%\Manassa
xcopy /S .\MultiAlignConsole\bin\x86\release\*  		%x86%\MultiAlignConsole

xcopy /S .\MultiAlignParameterFileEditor\bin\x64\release\*  	%x64%\MultiAlignParameterFileEditor
xcopy /S .\Manassa\bin\x64\release\*  				%x64%\Manassa
xcopy /S .\MultiAlignConsole\bin\x64\release\*  		%x64%\MultiAlignConsole

ECHO Cleaning up directories
del /F /S %x86%\MultiAlignParameterFileEditor\*.vshost.exe*
del /F /S %x86%\MultiAlignParameterFileEditor\*.pdb
del /F /S %x86%\Manassa\*.vshost.exe*
del /F /S %x86%\Manassa\*.pdb

rmdir /Q /S %x86%\MultiAlignConsole\examples
rmdir /Q /S %x86%\MultiAlignConsole\sic
rmdir /Q /S %x86%\MultiAlignConsole\scripts


del /F /S %x86%\MultiAlignConsole\*.vshost.exe*
del /F /S %x86%\MultiAlignConsole\*.pdb

del /F /S %x64%\MultiAlignParameterFileEditor\*.vshost.exe*
del /F /S %x64%\MultiAlignParameterFileEditor\*.pdb
del /F /S %x64%\Manassa\*.vshost.exe*
del /F /S %x64%\Manassa\*.pdb
del /F /S %x64%\MultiAlignConsole\*.vshost.exe*
del /F /S %x64%\MultiAlignConsole\*.pdb

rmdir /Q /S %x64%\MultiAlignConsole\examples
rmdir /Q /S %x64%\MultiAlignConsole\sic
rmdir /Q /S %x64%\MultiAlignConsole\scripts
rmdir /Q /S %x64%\MultiAlignConsole\zh-cn

SET /P "isDone=DONE? "