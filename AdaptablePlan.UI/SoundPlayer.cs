using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AdaptablePlan.UI;

// Проигрывание звука окончания таски - только Windows, на других платформах
// тихо. Пустой или несуществующий файл - стандартный системный звук (алиас
// SystemExclamation). Wav - winmm PlaySound, mp3 - MCI (MPEGVideo): PlaySound
// mp3 не декодирует.
internal static class SoundPlayer
{
    [DllImport("winmm.dll")]
    private static extern bool PlaySound(string name, IntPtr hmod, int flags);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, StringBuilder? buffer, int bufferSize, IntPtr hwndCallback);

    private const int SND_ASYNC = 0x1;
    private const int SND_FILENAME = 0x20000;
    private const int SND_ALIAS = 0x40000;

    public static void Play(string? file)
    {
        if (!OperatingSystem.IsWindows())
            return;
        var path = file?.Trim();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            PlaySound("SystemExclamation", IntPtr.Zero, SND_ALIAS | SND_ASYNC);
            return;
        }
        if (Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            PlayMp3(path);
        else
            PlaySound(path, IntPtr.Zero, SND_FILENAME | SND_ASYNC);
    }

    private static void PlayMp3(string path)
    {
        // Предыдущий прогон закрываем - иначе open вернёт ошибку занятого алиаса.
        mciSendString("close planSound", null, 0, IntPtr.Zero);
        if (mciSendString($"open \"{path}\" alias planSound", null, 0, IntPtr.Zero) == 0)
            mciSendString("play planSound", null, 0, IntPtr.Zero);
    }
}
