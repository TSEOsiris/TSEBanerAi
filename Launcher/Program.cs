using System.Diagnostics;

class Program
{
    static void Main()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = @"C:\TSEBanerAi\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Bannerlord.exe",
            Arguments = "/singleplayer _MODULES_*Bannerlord.Harmony*Bannerlord.ButterLib*Bannerlord.UIExtenderEx*BetterExceptionWindow*Bannerlord.MBOptionScreen*Native*SandBoxCore*Sandbox*StoryMode*NavalDLC*BirthAndDeath*TSEBanerAi*_MODULES_",
            WorkingDirectory = @"C:\TSEBanerAi\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\"
        });
    }
}