# Koli

A Windows desktop application for real-time voice transcription using the OpenAI Whisper API (cloud) or a custom on-premise Whisper API. The application captures audio from your microphone, transcribes it to text, and can automatically type the result into whatever window is currently active.

## 📋 Table of Contents

- [Features](#-features)
- [Prerequisites](#-prerequisites)
- [Installation](#-installation)
- [Configuration](#️-configuration)
- [Usage](#-usage)
- [Keyboard Shortcuts](#keyboard-shortcuts)
- [Text Rewriting](#text-rewriting)
- [Project Structure](#️-project-structure)
- [Development](#-development)
- [Production Build](#-production-build)
- [Dependencies](#-dependencies)
- [Troubleshooting](#-troubleshooting)

---

## ✨ Features

| Feature | Description |
|---|---|
| 🎤 **Real-time audio capture** | Captures microphone audio via WASAPI (NAudio) at 16 kHz / 16-bit mono |
| 📝 **Speech-to-text** | Transcription via OpenAI Whisper API, Azure OpenAI, or an on-premise Whisper API |
| ✏️ **Professional rewriting** | Automatically rewrites transcribed text in a formal style using OpenAI GPT models |
| ⌨️ **Auto-typing** | Types transcribed (or rewritten) text directly into the active application window |
| 🔒 **Secure key storage** | API keys are encrypted with Windows DPAPI and stored in `Config/api.secret` |
| 🪟 **System tray** | Minimises to the system tray; double-click or right-click to restore |
| 🌐 **Multi-language** | Supports any language recognised by Whisper; language can follow the active keyboard layout automatically |
| 🔇 **Hallucination filter** | Detects and silently drops known Whisper hallucinations and repeated/garbled output |
| 🖥️ **Debug console** | Built-in console for inspecting every API request and response in real time |
| 🎨 **Modern UI** | Borderless Windows Forms window with a dark Violet/Indigo theme and Windows 11 Mica backdrop |

---

## 🔧 Prerequisites

- **Windows 10 / 11** (Windows Forms application; Mica backdrop requires Windows 11 22H2 or later)
- **.NET 8.0 SDK** (only required to build from source; the published executable is self-contained)
- A working **microphone**
- An **OpenAI API key** (standard OpenAI or Azure OpenAI) **or** access to a compatible on-premise Whisper API

---

## 📦 Installation

1. Clone the repository:

```bash
git clone <repo-url>
cd Koli
```

2. Restore NuGet packages:

```bash
dotnet restore
```

3. Configure your API key (see [Configuration](#️-configuration) below).

4. Run the application:

```bash
dotnet run
```

On first launch, if no `Config/appsettings.json` is present, a default configuration file will be created automatically and a dialog will prompt you to fill in your API key.

---

## ⚙️ Configuration

All settings live in `Config/appsettings.json`. The file is structured into four sections:

```json
{
  "AzureOpenAI": { ... },
  "Audio":        { ... },
  "Typing":       { ... },
  "Rewrite":      { ... },
  "Translation":  { ... }
}
```

### AzureOpenAI section

| Key | Type | Default | Description |
|---|---|---|---|
| `ApiKey` | string | `""` | Your OpenAI or Azure OpenAI API key. Automatically encrypted to `Config/api.secret` on first run; leave empty afterwards to use the stored key. |
| `Endpoint` | string | `""` | Leave empty for the public OpenAI API. Set to your Azure OpenAI resource endpoint (`https://{resource}.openai.azure.com`) or to a full on-premise URL to enable Hadassah/on-premise mode. |
| `Model` | string | `"whisper-1"` | Batch HTTP transcription: `"whisper-1"`, `"gpt-4o-transcribe"`, etc. **Realtime** (live streaming via WebSocket): `"gpt-realtime-whisper"` or `"gpt-realtime"` — requires the public OpenAI API (`Endpoint` empty or host `api.openai.com`); not available with on-premise URLs or Azure endpoints in this version. Choose from **Settings → Transcription model** in the app. |
| `Language` | string | `"en"` | ISO 639-1 language code sent to the API (e.g. `"en"`, `"fr"`, `"he"`). Overridden at runtime when `LanguageMode` is `"Auto"`. |
| `OmitTranscriptionLanguage` | bool | `false` | When `true`, no `language` field is sent to the API, allowing the model to handle mixed-script speech (e.g. French + Hebrew) without forcing a single language. |
| `Prompt` | string | (see below) | Optional prompt to guide transcription style or vocabulary. |
| `LanguageMode` | string | `"Auto"` | `"Auto"` — the language hint sent to Whisper follows the active keyboard layout. `"Manual"` — the hint is fixed to `ManualLanguage`. This is only an *input-language hint* for the transcriber; to change the output language use the [Translation section](#translation-section). |
| `ManualLanguage` | string | `"en"` | Language hint used when `LanguageMode` is `"Manual"`. Has no effect on the output language. |
| `TranscriptionPromptId` | int? | `null` | *On-premise only.* Prompt template ID for transcription. |
| `FormattingPromptId` | int? | `null` | *On-premise only.* Prompt template ID for post-transcription formatting. |
| `EnableSpeakerDiarization` | bool | `false` | *On-premise only.* Enable speaker diarization in the transcription response. |

#### OpenAI Realtime transcription (`gpt-realtime-whisper`, `gpt-realtime`)

These models connect with WebSocket (`wss://api.openai.com/v1/realtime?model=…`), which opens a **realtime** session; the app sends `session.update` with `session.type: "realtime"` and enables [`audio.input.transcription`](https://developers.openai.com/api/docs/guides/realtime-websocket) so transcript deltas arrive during recording in dictation and meeting modes. Audio is resampled from the app capture rate (16 kHz) to 24 kHz PCM per OpenAI’s session format. Meeting speaker separation still runs in the background via the existing **GPT-4o** diarization HTTP call (unchanged). OpenAI documents paid-tier quotas for Realtime; Azure OpenAI Realtime is not wired here yet.

#### Standard OpenAI example

```json
"AzureOpenAI": {
  "ApiKey": "YOUR_API_KEY_HERE",
  "Endpoint": "",
  "Model": "whisper-1",
  "Language": "en",
  "OmitTranscriptionLanguage": false,
  "Prompt": "This is a voice dictation transcription. Ignore noise and silence.",
  "LanguageMode": "Auto",
  "ManualLanguage": "en"
}
```

#### Azure OpenAI endpoint example

Set `Endpoint` to your resource endpoint (must contain `openai.com`):

```
"Endpoint": "https://my-resource.openai.azure.com"
```

#### On-premise (Hadassah) API example

If `Endpoint` is set to a URL that does **not** contain `openai.com`, the application automatically switches to the on-premise protocol:

- **Request**: multipart POST to the configured URL with an `x-api-key` header and form fields: WAV audio file, `language`, `transcriptionPromptId`, `formattingPromptId`, `enableSpeakerDiarization`, `externalUser`, etc.
- **Response**: JSON `{ "Success": true, "Content": "transcribed text", "ErrorMessage": null }`

```json
"AzureOpenAI": {
  "ApiKey": "YOUR_ON_PREMISE_API_KEY",
  "Endpoint": "https://your-server.example.com/api/AI/queryAudio",
  "TranscriptionPromptId": 11,
  "FormattingPromptId": 9,
  "EnableSpeakerDiarization": false,
  "Language": "fr"
}
```

---

### Audio section

```json
"Audio": {
  "SampleRate": 16000,
  "Device": "default"
}
```

| Key | Default | Description |
|---|---|---|
| `SampleRate` | `16000` | Sample rate in Hz. 16 000 Hz is recommended for Whisper. |
| `Device` | `"default"` | `"default"` uses the system default microphone. Provide a partial device name (case-insensitive) to select a specific device. |

---

### Typing section

```json
"Typing": {
  "AutoSpace": true,
  "ChunkDelayMs": 10,
  "TypeInActiveWindow": true,
  "StreamingMode": false
}
```

| Key | Default | Description |
|---|---|---|
| `AutoSpace` | `true` | Append a space after each transcription chunk when typing. |
| `ChunkDelayMs` | `10` | Delay in milliseconds between individual character events when typing. |
| `TypeInActiveWindow` | `true` | Automatically type the transcribed text into whatever window had focus when recording started. |
| `StreamingMode` | `false` | When `true`, text is typed progressively as chunks arrive. When `false`, the full result is typed at once after transcription completes. |

---

### Rewrite section

```json
"Rewrite": {
  "Enabled": false,
  "Model": "gpt-4o",
  "ProfessionalismLevel": "Professional",
  "Prompt": ""
}
```

| Key | Default | Description |
|---|---|---|
| `Enabled` | `false` | Enable or disable automatic rewriting. Can also be toggled from the Settings menu at runtime. |
| `Model` | `"gpt-4o"` | OpenAI chat model used for rewriting (e.g. `"gpt-3.5-turbo"`, `"gpt-4"`, `"gpt-4o"`). |
| `ProfessionalismLevel` | `"Professional"` | Predefined rewriting style. Ignored when a custom `Prompt` is set. See levels below. |
| `Prompt` | `""` | Custom system prompt. When non-empty, overrides `ProfessionalismLevel`. |

**Available professionalism levels:**

| Level | Description |
|---|---|
| `Casual` | Slightly polished, conversational tone |
| `Polished` | Clear and natural, approachable |
| `Professional` | Business-appropriate, formal (default) |
| `Formal` | Highly formal, sophisticated vocabulary |
| `Executive` | Corporate, authoritative style |

> **Note:** Rewriting uses the same OpenAI API key as transcription and consumes additional tokens.

---

### Translation section

```json
"Translation": {
  "Enabled": false,
  "TargetLanguage": "",
  "Endpoint": "",
  "Model": "gpt-4o-mini"
}
```

By default the application transcribes audio in whatever language the speaker uses. When you want the output in a *different* language, opt in by enabling translation and picking a target language (either from the **Translation Settings…** menu or directly in the config file). This is independent from `LanguageMode`, which only hints the transcription API about the **input** language.

| Key | Default | Description |
|---|---|---|
| `Enabled` | `false` | Master switch. When `true` **and** `TargetLanguage` is non-empty, a chat-completion call is made after transcription to translate the text into `TargetLanguage`. |
| `TargetLanguage` | `""` | ISO 639-1 code of the desired output language (e.g. `"en"`, `"fr"`, `"he"`). Leave empty to disable translation regardless of `Enabled`. |
| `Endpoint` | `""` | Chat-completion URL. Leave empty to auto-derive from `AzureOpenAI.Endpoint`: OpenAI/Azure endpoint → `https://api.openai.com/v1/chat/completions`; Ai Nexus / on-premise endpoint (`.../api/AI/queryAudio`) → sibling `/api/ai/query`. Override to point at any compatible endpoint. |
| `Model` | `"gpt-4o-mini"` | Only used for the OpenAI / Azure path. Ignored for Ai Nexus (server picks its default provider/model). |

> **Why is this separate from `LanguageMode`?** Whisper / `gpt-4o-transcribe`'s `language` parameter is only an *input-language hint* — the model still transcribes in whatever language is actually spoken. To change the **output** language (e.g. speak French → write English), a real translation step is required. The same API key configured for transcription is reused.

### Secure API key storage

On first run the value of `ApiKey` is encrypted using the Windows Data Protection API (DPAPI) and saved as `Config/api.secret`. On subsequent launches you can leave `ApiKey` blank; the application will decrypt and use the stored key automatically. To rotate the key, update `ApiKey` in `appsettings.json` and delete `Config/api.secret`.

### ⚠️ Security notice

**Never commit `appsettings.json` to source control if it contains a real API key.** The `.gitignore` is pre-configured to optionally exclude it (uncomment the relevant line). Use `appsettings_openai.json` and `appsettings_hadassah.json` as reference templates.

---

## 🚀 Usage

### Quick start

1. **Launch** the application (`Koli.exe` or `dotnet run`).
2. Click the large **microphone button** in the centre — or press **F9** — to start recording. The status indicator changes to *Recording…* and the waveform visualiser animates.
3. **Speak** into your microphone. Audio is sent to the configured API in 4-second chunks.
4. Press **F9** again (or click the button) to stop. The transcription is finalised, optionally rewritten, copied to the clipboard, and typed into the target window.
5. Press **F7** to cancel the current recording without transcribing.
6. Press **F6** to pause/resume recording mid-session.

### Keyboard Shortcuts

These hotkeys work **globally** (even when the application is minimised or in the system tray):

| Key | Action |
|---|---|
| **F9** | Start / stop recording |
| **F7** | Cancel current recording (no transcription) |
| **F6** | Pause / resume recording |

### Translation

By default the application writes what you say, in the language you said it. The **chip at the top-left of the window** now controls translation:

- When translation is **off** (default) the chip shows `Off`.
- Click the chip to open the **Translation** dialog, check *Translate transcription into another language*, pick a target (**English**, **French**, or **Hebrew**), and click **Save**. The chip then shows the target language code (e.g. `EN`).
- Uncheck the option in the dialog to return to same-language transcription.

From that point on, every transcription is sent through a chat-completion call and the translated text is typed/copied instead of the raw transcription. The same API key (OpenAI or Ai Nexus) is reused — no extra configuration needed.

> The `LanguageMode` / `ManualLanguage` fields in `appsettings.json` remain available as advanced, config-only options: they are only hints to the transcriber about the **input** language and do not affect the output language.

### Text Rewriting

Open the **Settings menu** (⚙ button, top-right) to enable or configure the rewriting feature:

1. **Enable/Disable**: toggle *Enable Text Rewriting* in the menu.
2. **Change style**: open *Rewriting Settings* to select a professionalism level or enter a custom prompt.
3. **How it works**: after each transcription the text is sent to the OpenAI Chat Completions API with a system prompt derived from the selected level. The rewritten text is then typed/copied instead of the raw transcription.

### System tray

- **Closing** the window (✕ button) hides it to the tray rather than quitting; a toast notification confirms this.
- **Minimising** sends the window to the taskbar.
- **Double-click** the tray icon to restore the window.
- **Right-click** the tray icon for *Show* / *Quit* options.

### Debug console

Open **Settings menu → Debug Console** to view a real-time log of every API request and response (method, URL, status code, body). API keys are automatically redacted. You can clear, copy, or export the log from within the console.

---

## 🏗️ Project Structure

```
Koli/
├── Config/
│   ├── AppSettings.cs            # Configuration model classes
│   ├── SecureSettingsStore.cs    # DPAPI-based API key encryption/decryption
│   ├── appsettings.json          # Active configuration file
│   ├── appsettings_openai.json   # Reference config for OpenAI / Azure OpenAI
│   └── appsettings_hadassah.json # Reference config for on-premise API
├── Services/
│   ├── AudioCaptureService.cs    # WASAPI microphone capture (NAudio), pause/resume support
│   ├── SpeechToTextService.cs    # Transcription via OpenAI or on-premise API; hallucination filter
│   ├── TextRewriteService.cs     # Professional rewriting via OpenAI Chat Completions
│   └── AsyncEnumerableContent.cs # Streaming multipart HTTP content helper
├── UI/
│   ├── MainForm.cs               # Main window; dark Fluent Design UI, hotkey registration
│   └── DebugConsole.cs           # Real-time API request/response log window
├── Resources/
│   ├── Koli.ico      # Application icon (embedded resource)
│   └── Koli.png      # Application image
├── Program.cs                    # Entry point; config loading, secure store initialisation
└── Koli.csproj       # Project file (.NET 8.0, Windows, self-contained)
```

---

## 💻 Development

### Requirements

- .NET 8.0 SDK
- Visual Studio 2022, Visual Studio Code, or JetBrains Rider
- Git

### Run from source

```bash
dotnet run
```

Or, with an explicit build step:

```bash
dotnet build
dotnet run --no-build
```

In development the application looks for `Config/appsettings.json` relative to the executable output directory. The `.csproj` is already configured to copy the file automatically (`<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`).

---

## 🔨 Production Build

Build a self-contained, single-file executable for Windows x64:

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The output is placed in:

```
bin/Release/net8.0-windows/win-x64/publish/Koli.exe
```

**Deployment checklist:**
- Place `Koli.exe` in any folder.
- Create a `Config/` subfolder next to the executable and put your `appsettings.json` inside it.
- On first launch, the API key is encrypted and stored as `Config/api.secret`.

**Build options reference:**

| Option | Description |
|---|---|
| `-c Release` | Optimised Release build |
| `-r win-x64` | Windows 64-bit runtime identifier |
| `--self-contained true` | Bundle the .NET runtime inside the executable |
| `/p:PublishSingleFile=true` | Produce a single `.exe` file |

---

## 📚 Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Extensions.Hosting` | 8.0.0 | Hosting infrastructure |
| `Microsoft.Windows.Compatibility` | 8.0.0 | Windows-specific APIs |
| `NAudio` | 2.2.1 | WASAPI microphone capture, WAV header writing |

---

## 🐛 Troubleshooting

### Application does not start
- Ensure `Config/appsettings.json` exists (created automatically on first run).
- Check that `ApiKey` and, if applicable, `Endpoint` are correctly set.
- If not using the self-contained build, verify that .NET 8.0 Runtime is installed.

### No transcription output
- Confirm your microphone is working and not in use by another application.
- Check the status bar for error messages.
- Open **Settings → Debug Console** to inspect the raw API response.
- Verify the API key is valid and has not exceeded its quota.

### Transcription API errors
- **OpenAI / Azure**: ensure the endpoint format is `https://{resource}.openai.azure.com` and the specified model is deployed.
- **On-premise**: ensure the endpoint URL does **not** contain `openai.com` and that the server is reachable.
- Check your internet connection and firewall rules.

### Audio issues
- Try setting `Device` to `"default"` in `appsettings.json`.
- Verify microphone permissions in **Windows Settings → Privacy → Microphone**.
- If audio is clipped or distorted, confirm `SampleRate` is `16000`.

### Auto-typing not working
- Ensure `TypeInActiveWindow` is `true`.
- Click in the target window **before** starting recording so the application captures the target window handle.
- Some applications running as Administrator may block synthetic keyboard input from a normal-privilege process.

### API key lost or not recognised
- Delete `Config/api.secret` and set `ApiKey` again in `appsettings.json`; it will be re-encrypted on next launch.
- The encrypted secret is bound to the current Windows user account (DPAPI `CurrentUser` scope) and cannot be used on a different machine or user account.

---

## 📝 Notes

- The application runs on **Windows only** (Windows Forms + WASAPI + DPAPI).
- API keys are never stored in plain text after the first run.
- Author: **Aharon Bensadoun** — Copyright © 2026. All rights reserved.
