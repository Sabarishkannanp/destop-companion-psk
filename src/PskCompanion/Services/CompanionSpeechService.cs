using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PskCompanion.Services;

public sealed class CompanionSpeechService : IDisposable
{
    private readonly SpeechSynthesizer? synthesizer;
    private readonly MediaPlayer sound = new();
    public bool IsSpeaking => isSpeaking;
    private bool isSpeaking;

    public CompanionSpeechService()
    {
        try
        {
            synthesizer = new SpeechSynthesizer();
            synthesizer.Rate = 1; // Natural speaking rate
            synthesizer.Volume = 95;
            synthesizer.SpeakCompleted += (_, _) => isSpeaking = false;
        }
        catch (Exception)
        {
            synthesizer = null;
        }

        var soundPath = Path.Combine(PskCompanion.Services.AssetPaths.Root, "pop.wav");
        if (File.Exists(soundPath))
        {
            sound.Open(new Uri(soundPath));
            sound.Volume = 0.4;
        }
    }

    public void PlayPop()
    {
        try
        {
            sound.Position = TimeSpan.Zero;
            sound.Play();
        }
        catch (Exception) { }
    }

    public async Task SpeakAsync(string text)
    {
        if (synthesizer == null || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            await Task.Run(() =>
            {
                lock (synthesizer)
                {
                    synthesizer.SpeakAsyncCancelAll();
                    isSpeaking = true;
                    synthesizer.Speak(text);
                }
            });
        }
        catch (Exception)
        {
            isSpeaking = false;
        }
    }

    public Task GreetGoodMorning(string name)
    {
        var hour = DateTime.Now.Hour;
        string greeting = hour < 12 ? "Good morning" : (hour < 17 ? "Good afternoon" : "Good evening");
        return SpeakAsync($"{greeting}, {name}! Time to work and crush your goals today!");
    }

    public Task TellToWork(string name, string? taskTitle = null)
    {
        if (!string.IsNullOrWhiteSpace(taskTitle))
        {
            return SpeakAsync($"Time to work, {name}! Let's focus on: {taskTitle}!");
        }
        return SpeakAsync($"Time to work, {name}! Let's get things done!");
    }

    public Task CelebrateTask(string name, string taskTitle)
    {
        PlayPop();
        return SpeakAsync($"Fantastic job, {name}! You completed: {taskTitle}! Keep up the momentum!");
    }

    public void Stop()
    {
        try
        {
            synthesizer?.SpeakAsyncCancelAll();
        }
        catch (Exception) { }
    }

    public void Dispose()
    {
        synthesizer?.Dispose();
    }
}

