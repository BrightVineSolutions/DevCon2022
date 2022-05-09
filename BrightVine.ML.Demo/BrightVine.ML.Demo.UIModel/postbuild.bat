xcopy "%~dp0htmlforms\*.*" "D:\Environments\BBCRM40SP30\APP\bbappfx\vroot\bin\custom\" /e /y /r
xcopy "%~dp0bin\debug\BrightVine.ML.Demo.UIModel.DLL" "D:\Environments\BBCRM40SP30\APP\bbappfx\vroot\browser\htmlforms\" /e /y /r

rem echo minify the html and js files to optimize their payload on the wire...
rem %~dp0..\..\..\..\Utils\Blackbaud.AppFx.JSMinifier\bin\JSMinifier.exe %~dp0..\..\..\..\Blackbaud.AppFx.Server\Deploy\browser\htmlforms\<subfolder>\*.html /pre
rem %~dp0..\..\..\..\Utils\Blackbaud.AppFx.JSMinifier\bin\JSMinifier.exe %~dp0..\..\..\..\Blackbaud.AppFx.Server\Deploy\browser\htmlforms\<subfolder>\*.js