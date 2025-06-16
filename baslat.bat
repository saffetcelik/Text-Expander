@echo off
title Otomatik Metin Genisletici Baslatici
color 0A

echo.
echo ========================================
echo   Otomatik Metin Genisletici
echo   Gelistirici: Saffet Celik
echo ========================================
echo.

REM Proje dizinine git
cd /d "%~dp0"

REM .NET 8 kurulu mu kontrol et
echo .NET 8 kontrol ediliyor...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo.
    echo HATA: .NET 8 Runtime bulunamadi!
    echo Lutfen .NET 8 Runtime'i yukleyin:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    echo Kurulum sonrasi bu dosyayi tekrar calistirin.
    echo.
    pause
    exit /b 1
)

REM .NET versiyonunu göster
echo .NET versiyon:
dotnet --version
echo.

REM Proje dosyası var mı kontrol et
if not exist "OtomatikMetinGenisletici.csproj" (
    echo HATA: Proje dosyasi bulunamadi!
    echo Bu batch dosyasini proje klasorunde calistirin.
    echo.
    pause
    exit /b 1
)

echo Program baslatiliyor...
echo Lutfen bekleyin, ilk calistirma biraz zaman alabilir...
echo.

REM Programi calistir
dotnet run --project OtomatikMetinGenisletici.csproj --verbosity quiet

REM Hata durumunda mesaj goster
if errorlevel 1 (
    echo.
    echo ========================================
    echo HATA: Program baslatilirken sorun olustu!
    echo ========================================
    echo.
    echo Olasi cozumler:
    echo 1. Proje dosyalarini kontrol edin
    echo 2. "dotnet restore" komutunu calistirin
    echo 3. "dotnet build" ile derlemeyi test edin
    echo.
    echo Hata detaylari icin asagidaki komutu calistirin:
    echo dotnet run --project OtomatikMetinGenisletici.csproj
    echo.
    pause
) else (
    echo.
    echo Program basariyla kapatildi.
    echo.
)
