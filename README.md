<div align="center">

# Koli

**Real-time speech transcription & meeting assistant for Windows**

A modern Windows desktop application that captures your voice or system audio,
transcribes it in real time via OpenAI Whisper / `gpt-4o-transcribe` / `gpt-realtime`
(or a compatible on-premise endpoint), optionally rewrites and translates the result,
and types it straight into the active application — with a built-in **meeting mode**
featuring multi-speaker diarization.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![Version](https://img.shields.io/badge/version-1.0.1.3-7C3AED)](https://github.com/Aharon-Bensadoun/Koli/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-22C55E)](LICENSE)

[Features](#-features) •
[Installation](#-installation) •
[Configuration](#-configuration) •
[Usage](#-usage) •
[Meeting Mode](#-meeting-mode) •
[Troubleshooting](#-troubleshooting)

</div>

---

## 📋 Table of Contents

- [Features](#-features)
- [Prerequisites](#-prerequisites)
- [Installation](#-installation)
- [Configuration](#-configuration)
- [Usage](#-usage)
  - [Keyboard Shortcuts](#keyboard-shortcuts)
  - [Output language](#output-language)
  - [Text Rewriting](#text-rewriting)
  - [Pending recordings (failed transcriptions)](#pending-recordings-failed-transcriptions)
  - [System Tray](#system-tray)
  - [Debug Console](#debug-console)
- [Meeting Mode](#-meeting-mode)
- [Project Structure](#-project-structure)
- [Development](#-development)
- [Production Build](#-production-build)
- [Dependencies](#-dependencies)
- [Troubleshooting](#-troubleshooting)
- [Roadmap](#-roadmap)
- [Contributing](#-contributing)
- [License](#-license)
- [Author & Contact](#-author--contact)

---

## ✨ Features

| Feature | Description |
|---|---|
| 🎤 **Real-time audio capture** | Captures microphone audio via WASAPI (NAudio) at 16 kHz / 16-bit mono. |
| 📝 **Speech-to-text** | Transcription via OpenAI Whisper, `gpt-4o-transcribe`, Azure OpenAI, OpenAI **Realtime WebSocket** (`gpt-realtime-whisper`, `gpt-realtime`), or a compatible on-premise endpoint. |
| 👥 **Meeting mode** | Multi-speaker meetings with **GPT-4o diarization**, microphone and/or system audio capture, participant management, live colorized transcript, and export to TXT / Markdown / JSON. |
| 🔊 **System audio capture** | Records what comes out of your speakers (WASAPI loopback) — perfect for transcribing Zoom / Teams / Meet calls without virtual cables. |
| ✏️ **Professional rewriting** | Automatically rewrites transcribed text in a chosen formality level using OpenAI chat models. |
| 🌐 **Output language** | Choose output language (OpenAI/Azure): native STT routing or transparent LLM fallback. On-prem: spoken language only. |
| ⌨️ **Auto-typing** | Types transcribed (or rewritten / translated) text directly into the active application window. |
| 🗣️ **Voice assistant (Alt Gr)** | Spoken question → OpenAI Responses API (optional web search) → direct answer typed into the active window. |
| 🔒 **Secure key storage** | API keys are encrypted with Windows DPAPI and stored in `Config/api.secret`. |
| 🪟 **System tray** | Minimises to the system tray; double-click or right-click to restore. |
| 🌍 **Multi-language** | Supports any language recognised by Whisper; the language hint can follow the active keyboard layout automatically. |
| 🔇 **Hallucination filter** | Detects and silently drops known Whisper hallucinations and repeated/garbled output. |
| 🖥️ **Debug console** | Built-in console for inspecting every API request and response in real time, with API keys redacted. |
| 🎨 **Modern UI** | WinUI 3 unpackaged shell with dark violet theme, Windows 11 Mica backdrop, and NavigationView (Home / History / Meeting / Debug / Settings). |
| 💾 **Resilient transcription failures** | If the transcription call fails (network outage, invalid API key, API 4xx/5xx, empty response), the captured audio is preserved as a WAV file and surfaced in the History view, where it can be played back, retried, or deleted. |

---

## 🔧 Prerequisites

- **Windows 10 / 11** (WinUI 3 unpackaged app; Mica backdrop requires Windows 11 22H2 or later for best results).
- **.NET 8.0 SDK** — only required to build from source. The published executable is self-contained and ships with the runtime.
- A working **microphone** (and/or any audio output device for system-audio capture in meeting mode).
- An **OpenAI API key** (standard OpenAI or Azure OpenAI) **or** access to a compatible on-premise Whisper API.

---

## 📦 Installation

### Option A — Install from MSIX package (recommended)

Download the latest `.msix` package from [Releases](https://github.com/Aharon-Bensadoun/Koli/releases).

1. **Install the signing certificate** (first time only):
   - Double-click `Koli.WinUI_1.0.1.2_x64.cer`
   - Click **Install Certificate…** → **Local Machine** → **Place all certificates in the following store** → **Browse** → **Trusted People** → **OK** → **Next** → **Finish**.
   - Or run in an elevated PowerShell:
     ```powershell
     Import-Certificate -FilePath .\Koli.WinUI_1.0.1.2_x64.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
     ```

2. **Install the app**:
   - Double-click `Koli.WinUI_1.0.1.2_x64.msix` and click **Install** (requires a **new** version if the app is already installed).
   - Or run the included script (supports reinstalling the same version via `Install.ps1` after a `Publish-Koli.ps1` build):
     ```powershell
     .\Install.ps1
     ```

3. Launch **Koli** from the Start menu. On first launch, a dialog prompts you to enter your API key.

### Option B — Install from source

1. **Clone the repository**

   ```bash
   git clone https://github.com/Aharon-Bensadoun/Koli.git
   cd Koli
   ```

2. **Restore NuGet packages**

   ```bash
   dotnet restore
   ```

3. **Configure your API key** — see [Configuration](#-configuration) below.

4. **Run the application**

   ```bash
   dotnet run --project Koli.WinUI/Koli.WinUI.csproj
   ```

On first launch, if no `Config/appsettings.json` exists, a default configuration file is created automatically and a dialog prompts you to enter your API key.

---

## ⚙️ Configuration

All settings live in `Config/appsettings.json`. The file is structured into the following sections:

```json
{
  "AzureOpenAI": { ... },
  "Audio":       { ... },
  "Typing":      { ... },
  "Rewrite":     { ... },
  "Meeting":     { ... },
  "Translation": { ... },
  "Assistant":   { ... }
}
```

### `AzureOpenAI` section

| Key | Type | Default | Description |
|---|---|---|---|
| `ApiKey` | string | `""` | Your OpenAI / Azure OpenAI / on-premise API key. Automatically encrypted to `Config/api.secret` on first run; leave empty afterwards to use the stored key. |
| `Endpoint` | string | `""` | Leave empty for the public OpenAI API. Set to your Azure OpenAI resource endpoint (`https://{resource}.openai.azure.com`) or to a full on-premise URL to enable on-premise mode. |
| `Model` | string | `"whisper-1"` | HTTP transcription: `"whisper-1"`, `"gpt-4o-transcribe"`, etc. **Realtime WebSocket**: `"gpt-realtime-whisper"` or `"gpt-realtime"` (public OpenAI endpoint only — see below). Pickable from **Settings → Transcription model**. |
| `Language` | string | `"en"` | ISO 639-1 language hint sent to the API (e.g. `"en"`, `"fr"`, `"he"`). Overridden at runtime when `LanguageMode` is `"Auto"`. |
| `OmitTranscriptionLanguage` | bool | `false` | When `true`, no `language` field is sent to the API, allowing the model to handle mixed-script speech (e.g. French + Hebrew) without forcing a single language. |
| `Prompt` | string | *(default voice-dictation prompt)* | Optional prompt to guide transcription style or vocabulary. |
| `LanguageMode` | string | `"Auto"` | `"Auto"` — language hint follows the active keyboard layout. `"Manual"` — hint is fixed to `ManualLanguage`. Note: this is only an *input-language hint*; to change the **output** language, see [Output language](#translation-section-output-language). |
| `ManualLanguage` | string | `"en"` | Language hint used when `LanguageMode` is `"Manual"`. |
| `ProviderId` | int? | `null` | *Ai Nexus on-premise only.* Multipart field `providerId` sent to `queryAudio`. |
| `TranscriptionPromptId` | int? | `null` | *On-premise only.* Prompt template ID for transcription. |
| `FormattingPromptId` | int? | `null` | *On-premise only.* Prompt template ID for post-transcription formatting. |
| `EnableSpeakerDiarization` | bool | `false` | *On-premise only.* Enable speaker diarization on the server side. |
| `EnableStreamingTranscription` | bool | `false` | *On-premise only.* When `true`, use WebSocket live STT (`/api/ai/realtime/transcribe`) in meeting and dictation (see [On-prem live transcription](#on-prem-live-transcription-ai-nexus)). |
| `RealtimeEndpoint` | string | `""` | *On-prem only.* Optional `wss://…/api/ai/realtime/transcribe` URL; empty derives from `Endpoint`. |
| `UseQueryAudioHttpStreamingFallback` | bool | `false` | *On-prem only.* If WebSocket connect fails, fall back to rolling `queryAudio` HTTP streaming. |
| `StreamingEndpoint` | string | `""` | *HTTP fallback only.* Optional `queryAudio` URL for fallback POSTs; empty uses `Endpoint`. |
| `StreamingProviderId` | int? | `null` | *On-prem live.* Optional `providerId` for WebSocket `start` / HTTP fallback; empty uses `ProviderId`. |

#### OpenAI Realtime transcription (`gpt-realtime-whisper`, `gpt-realtime`)

These models connect over WebSocket (`wss://api.openai.com/v1/realtime?model=…`) and open a **realtime** session. The app sends `session.update` with `session.type: "realtime"` and enables [`audio.input.transcription`](https://developers.openai.com/api/docs/guides/realtime-websocket), so transcript deltas arrive *during* recording in both dictation and meeting modes. Audio is resampled from the capture rate (16 kHz) to 24 kHz PCM as required by OpenAI's session format. Meeting speaker separation still runs in the background through the GPT-4o diarization HTTP call (unchanged).

> ⚠️ OpenAI Realtime WebSocket (`wss://api.openai.com/v1/realtime`) is wired only for the public OpenAI endpoint. Azure OpenAI Realtime falls back to chunked HTTP.

#### On-prem live transcription (Ai Nexus)

When `Endpoint` does not contain `openai.com` and `EnableStreamingTranscription` is `true`, Koli uses the **Ai Nexus WebSocket** documented by Hadassah:

- **URL**: `wss://{host}/api/ai/realtime/transcribe` (derived from `Endpoint`, or set `RealtimeEndpoint` explicitly).
- **Auth**: `x-api-key` header at handshake (same key as batch `queryAudio`).
- **Protocol**: JSON messages — `start` (`providerId`, `language` / `auto`, `externalUser`), `audio` (base64 PCM16 mono **24 kHz**), `stop`; server sends `session_ready`, `partial`, `final`, `error`, `done`.
- **Capture**: microphone PCM at 16 kHz, resampled to 24 kHz before send (~100 ms chunks).

Enable the toggle in **Settings → API configuration** when an on-prem endpoint is configured, or set `EnableStreamingTranscription` in `appsettings.json`. **Meeting** and **dictation (Home)** both use this live path when enabled.

**Optional HTTP fallback** (`UseQueryAudioHttpStreamingFallback`): if the WebSocket cannot connect, Koli can fall back to legacy rolling **POST** `queryAudio` with `stream=true` (SSE/NDJSON), using `StreamingEndpoint` when set.

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

#### Azure OpenAI example

Set `Endpoint` to your Azure resource endpoint (host must contain `openai.com`):

```json
"Endpoint": "https://my-resource.openai.azure.com"
```

#### On-premise (Ai Nexus / Hadassah) example

If `Endpoint` is set to a URL that does **not** contain `openai.com`, the application automatically switches to the on-premise protocol:

- **Request**: multipart POST to the configured URL with an `x-api-key` header and form fields: WAV audio file, `language`, `transcriptionPromptId`, `formattingPromptId`, `enableSpeakerDiarization`, `externalUser`, `providerId`, etc.
- **Response**: JSON `{ "Success": true, "Content": "transcribed text", "ErrorMessage": null }`.

```json
"AzureOpenAI": {
  "ApiKey": "YOUR_ON_PREMISE_API_KEY",
  "Endpoint": "https://your-server.example.com/api/AI/queryAudio",
  "TranscriptionPromptId": 11,
  "FormattingPromptId": 9,
  "EnableSpeakerDiarization": false,
  "EnableStreamingTranscription": true,
  "RealtimeEndpoint": "",
  "UseQueryAudioHttpStreamingFallback": false,
  "StreamingEndpoint": "",
  "StreamingProviderId": null,
  "Language": "fr"
}
```

### `Audio` section

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

### `Typing` section

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

### `Rewrite` section

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
| `Casual` | Slightly polished, conversational tone. |
| `Polished` | Clear and natural, approachable. |
| `Professional` | Business-appropriate, formal (default). |
| `Formal` | Highly formal, sophisticated vocabulary. |
| `Executive` | Corporate, authoritative style. |

> **Note:** Rewriting reuses the same OpenAI API key as transcription and consumes additional tokens.

### `Meeting` section

```json
"Meeting": {
  "DefaultAudioSource": "Microphone",
  "ChunkDurationSeconds": 6,
  "TranscriptSavePath": "Meetings",
  "AutoSaveTranscript": true
}
```

| Key | Default | Description |
|---|---|---|
| `DefaultAudioSource` | `"Microphone"` | Audio source preselected when opening Meeting mode. One of `"Microphone"`, `"SystemAudio"`. The combined "Mic + System Audio" option is also available in the UI. |
| `ChunkDurationSeconds` | `6` | Length of each audio chunk sent for HTTP transcription. Used for on-prem **HTTP fallback** windows and OpenAI periodic commits. Ignored for OpenAI / on-prem WebSocket live transcription. |
| `TranscriptSavePath` | `"Meetings"` | Folder (relative to the executable) where transcripts are saved. |
| `AutoSaveTranscript` | `true` | When `true`, the transcript is automatically saved (TXT + JSON) when the meeting ends. |

### `Translation` section (output language)

```json
"Translation": {
  "Enabled": false,
  "TargetLanguage": "",
  "Mode": "SameAsSpoken",
  "Endpoint": "",
  "Model": "gpt-4o-mini"
}
```

Controls the **written output language**, separate from `LanguageMode` / `ManualLanguage`, which only hint the **spoken** language to the transcriber.

| Key | Default | Description |
|---|---|---|
| `Mode` | `"SameAsSpoken"` | `"SameAsSpoken"` — transcribe in the spoken language. `"Fixed"` — request output in `TargetLanguage` (**OpenAI / Azure OpenAI endpoints only**). |
| `TargetLanguage` | `""` | ISO 639-1 output code when `Mode` is `"Fixed"` (e.g. `"en"`, `"fr"`, `"he"`). |
| `Enabled` | `false` | Legacy flag kept in sync with `Mode` for older config files. At load time, `Enabled=true` with a non-empty `TargetLanguage` migrates to `Mode=Fixed`. |
| `Endpoint` | `""` | Chat-completion URL for LLM fallback. Empty → derived from `AzureOpenAI.Endpoint`. |
| `Model` | `"gpt-4o-mini"` | OpenAI / Azure chat model for fallback translation. |
| `ProviderId` | `null` | *Ai Nexus only.* `providerId` for `/api/ai/query`. |

**OpenAI / Azure strategies** (automatic, no extra UI step):

| Situation | Strategy |
|---|---|
| Output = spoken language | `/v1/audio/transcriptions` |
| Output = English, model `whisper-1` | `/v1/audio/translations` (native audio translation) |
| Output ≠ spoken, model `gpt-4o-transcribe` | `/v1/audio/transcriptions` + enriched prompt |
| Realtime WebSocket, cross-lingual | Realtime STT + LLM translation per final segment |
| Other OpenAI/Azure HTTP pairs | Transcription then LLM fallback |

**On-premise endpoints** (URL without `openai.com`, e.g. Ai Nexus `queryAudio`): output-language routing is **disabled**. STT behaviour is unchanged (text in the spoken language). The Home **Output** chip is disabled with an explanatory tooltip. Legacy `Enabled` + `TargetLanguage` still triggers post-transcription LLM translation on on-prem if configured.

**Limitations:** `/audio/translations` targets **English only** with `whisper-1`. Realtime cross-lingual output may add slight latency. Prompt-based output on `gpt-4o-transcribe` is best-effort; LLM fallback guarantees the target language when needed.

### Secure API key storage

On first run, the value of `ApiKey` is encrypted using the Windows Data Protection API (DPAPI) and saved as `Config/api.secret`. On subsequent launches you can leave `ApiKey` blank; the application will decrypt and use the stored key automatically. To rotate the key, update `ApiKey` in `appsettings.json` and delete `Config/api.secret`.

### ⚠️ Security notice

**Never commit `appsettings.json` to source control if it contains a real API key.** The `.gitignore` is pre-configured to optionally exclude it — uncomment the relevant line:

```gitignore
# Configuration files with sensitive data
Config/appsettings.json
```

The encrypted secret in `Config/api.secret` is bound to the current Windows user account (DPAPI `CurrentUser` scope) and cannot be used on a different machine or user account.

---

## 🚀 Usage

### Quick start (dictation mode)

1. **Launch** the application (`Koli.exe` or `dotnet run`).
2. Click the large **microphone button** at the centre of the Home view — or press **F9** — to start recording. The status indicator changes to *Recording…* and the waveform visualiser animates.
3. **Speak** into your microphone. Audio is sent to the configured API in 4-second chunks (or streamed live for Realtime models).
4. Press **F9** again (or click the button) to stop. The transcription is finalised, optionally rewritten and translated, copied to the clipboard, and typed into the target window.
5. Press **F7** to cancel the current recording without transcribing.
6. Press **F6** to pause/resume recording mid-session.

### Keyboard Shortcuts

These hotkeys work **globally** — even when the application is minimised or in the system tray:

| Key | Action |
|---|---|
| **F9** | Start / stop dictation recording |
| **Alt Gr** | Start / stop **voice assistant** (spoken question → direct answer) |
| **F7** | Cancel current recording (no transcription) |
| **F6** | Pause / resume recording |

### Voice assistant (Alt Gr)

Press **Alt Gr** once to start recording a spoken question, and again to stop. Koli transcribes the audio (batch STT only — no live Realtime stream), sends the text to the OpenAI **Responses** API with optional **web search**, and pastes/types **only the final answer** into the active window (no conversational formatting, no citations).

Configure under **Settings → Voice assistant (Alt Gr)**:

| Setting | Default | Description |
|---|---|---|
| `Assistant.Enabled` | `true` | Master switch for Alt Gr assistant |
| `Assistant.Model` | `gpt-4.1` | Model for Responses / fallback chat |
| `Assistant.WebSearchEnabled` | `true` | Use `web_search` tool when supported |
| `Assistant.SystemPrompt` | (French, direct-answer only) | Instructions sent with each query |

**Requirements:** a valid OpenAI API key and a **public OpenAI** endpoint (`api.openai.com` — empty `AzureOpenAI.Endpoint` is fine). Azure/on-prem STT endpoints are supported for transcription, but the assistant LLM step requires OpenAI cloud. If Responses or web search fails, Koli falls back to chat completions without web search.

**History** entries are stored as `Q: …` / `A: …`. Expect **5–15 s** latency (STT + web search). Web search may incur additional API tool costs.

### Output language

The Home bar shows **Output** and **Input** (from the Windows keyboard in Auto mode):

- **Output: Auto** — text matches the spoken language (default).
- Click **Output** to open the output-language dialog: *Same as spoken* or a fixed language (English, French, עברית, … or custom ISO code).
- With OpenAI / Azure OpenAI, Koli routes STT to produce that language when possible (see config table above). Otherwise a silent LLM fallback runs; status stays *Transcribing…* rather than *Translating…*.
- On on-prem endpoints, **Output** is disabled (`N/A`); transcription stays in the spoken language.

Meeting mode shows a read-only **Output: …** badge when OpenAI / Azure is configured.

> `LanguageMode` / `ManualLanguage` in `appsettings.json` remain input-language hints only.

### Text Rewriting

Open the **Settings menu** (⚙ button, top-right) to enable or configure the rewriting feature:

1. **Enable / disable**: toggle *Enable Text Rewriting* in the menu.
2. **Change style**: open *Rewriting Settings* to select a professionalism level or enter a custom prompt.
3. **How it works**: after each transcription, the text is sent to the OpenAI Chat Completions API with a system prompt derived from the selected level. The rewritten text is then typed/copied instead of the raw transcription.

### Pending recordings (failed transcriptions)

When a dictation transcription fails — for example because the API key is invalid, the network dropped, the server returned a 4xx/5xx response, or no usable text was produced — Koli does **not** discard your recording. The raw PCM is written as a standard WAV file under `Config/PendingAudio/<id>.wav`, with a small JSON index (`Config/pending-audio.json`) so the list survives application restarts.

These recordings appear at the top of the **sidebar → History** view as amber "PENDING" cards, each showing the capture time, duration, language hint, and the last error message returned by the API. Three actions are available on every card:

| Icon | Action | Description |
|---|---|---|
| ▶ / ■ | **Play / Stop** | Plays the recording through your default playback device using NAudio. Only one recording plays at a time. |
| ⟳ | **Retry transcription** | Re-runs the transcription with the **currently configured** settings (model, endpoint, language, API key). On success, the resulting text is moved to the regular transcript history and the pending card disappears. On failure, the toast surfaces the new error and the card stays around with its error message updated. |
| 🗑 | **Delete** | Permanently removes the entry and its `.wav` file after confirmation. |

The pending list is capped at 50 entries (oldest evicted first) so it cannot grow without bound. You can also wipe the entire folder by hand at any time — Koli rebuilds its index from disk on next launch.

### System Tray

- **Closing** the window (✕ button) hides it to the tray rather than quitting; a toast notification confirms this.
- **Minimising** sends the window to the taskbar.
- **Double-click** the tray icon to restore the window.
- **Right-click** the tray icon for *Show* / *Quit* options.

### Debug Console

Open **sidebar → Debug** (or *Settings menu → Debug Console*) to view a real-time log of every API request and response (method, URL, status code, body). API keys are automatically redacted. You can clear, copy, or export the log from within the console.

---

## 👥 Meeting Mode

Meeting mode is dedicated to multi-speaker scenarios (interviews, stand-ups, calls, lectures). It is available from the **sidebar → Meeting** entry in the main window.

### What it does

- Captures audio from the **microphone**, the **system output** (WASAPI loopback), or **both combined**.
- Sends audio in real time to the configured STT model (OpenAI Realtime WebSocket, on-prem HTTP streaming with `stream=true`, or chunked HTTP batch).
- Runs a **GPT-4o diarization** pass over each chunk to label utterances with speaker IDs, preserving speaker consistency across the meeting.
- Displays a live transcript with **color-coded speakers** and timestamps.
- Lets you pre-declare participants (via the *Participants* dialog at the start of the meeting) so the model uses their real names instead of generic `Speaker 1 / 2 / 3`.
- Auto-saves the transcript and exports to **TXT**, **Markdown**, or **JSON** on demand.

### Starting a meeting

1. Open Koli and click **Meeting** in the left sidebar.
2. Type a **meeting title** and pick an **audio source**:
   - **Microphone** — your default mic.
   - **System Audio** — what comes out of your speakers/headphones (ideal for Zoom / Teams / Meet, no virtual cable needed).
   - **Mic + System Audio** — both streams mixed.
3. Click **▶ Start**. A *Participants* dialog appears: optionally type one name per line, then **OK**.
4. Speak (or play the audio). Transcript segments appear in real time, each attributed to a speaker and colorized accordingly.
5. Click **■ Stop** to end the meeting. The transcript is auto-saved to the folder configured by `Meeting.TranscriptSavePath` (default: `Meetings/`).
6. Click **Export** to save the transcript as TXT, Markdown, or JSON to a custom location.

### Notes

- Speaker labels are refined asynchronously: a segment may appear with a tentative speaker, then update once GPT-4o has more context. This is normal.
- Diarization always uses **GPT-4o** through the chat-completions endpoint, regardless of the transcription model in use.
- System-audio capture uses Windows WASAPI loopback at the default audio endpoint format and is downmixed/resampled to 16 kHz PCM internally.

---

## 🏗️ Project Structure

```
Koli/
├── Koli.sln
├── Koli.Core/                    # Config, Models, Services (no UI)
│   ├── Config/
│   ├── Models/
│   └── Services/
├── Koli.Platform/                # Win32 interop: hotkeys, tray, typing, toasts, input language
├── Koli.WinUI/                   # WinUI 3 unpackaged app (XAML + MVVM)
│   ├── Views/                    # Home, History, Meeting, Debug, Settings pages
│   ├── ViewModels/
│   ├── Dialogs/
│   ├── Themes/                   # Dark violet Fluent theme
│   └── Assets/                   # Koli.ico, Koli.png
├── Koli.Core.Tests/
└── README.md
```

Runtime data (next to the executable after first run):

```
Config/
├── appsettings.json
├── api.secret                    # DPAPI-encrypted API key
├── history.json
├── pending-audio.json
└── PendingAudio/                 # Failed-transcription WAV files
```

---

## 💻 Development

### Requirements

- .NET 8.0 SDK (8.0.402 or later; the repo pins **8.0.421** via `global.json` so WinUI builds do not use a .NET 9 SDK host by default)
- Visual Studio 2022, Visual Studio Code, or JetBrains Rider
- Git

### Run from source

```bash
dotnet build Koli.sln
dotnet run --project Koli.WinUI/Koli.WinUI.csproj
```

In development the application looks for `Config/appsettings.json` relative to the executable output directory. `Koli.Core` copies the template automatically (`CopyToOutputDirectory=PreserveNewest`).

### Code conventions

- C# 12 / .NET 8, nullable enabled.
- One service per file, dependency-injected through constructors (`Microsoft.Extensions.DependencyInjection` in `App.xaml.cs`).
- UI lives under `Koli.WinUI/` (XAML + ViewModels); business logic under `Koli.Core/Services/`; configuration under `Koli.Core/Config/`; platform interop under `Koli.Platform/`.
- API keys must **never** be logged or surfaced in exception messages; use `SecureSettingsStore` for persistence and `DebugLogService` redaction when logging requests.

---

## 🔨 Production Build

### MSIX package (recommended for distribution)

#### Signing certificate

MSIX builds are signed with a self-signed certificate referenced by thumbprint in `Koli.WinUI/Koli.WinUI.csproj`. If the build fails with `APPX0102` (certificate not found) or `APPX0107` (certificate not valid for signing), generate a new cert in PowerShell:

```powershell
$cert = New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=Aharon Bensadoun" `
  -KeyUsage DigitalSignature `
  -FriendlyName "Koli MSIX Signing" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$cert.Thumbprint
```

Copy the thumbprint and update `PackageCertificateThumbprint` in `Koli.WinUI/Koli.WinUI.csproj`.

Optionally export a `.cer` for end users (included in the release package):

```powershell
Export-Certificate -Cert $cert -FilePath ".\Koli.WinUI.cer"
```

Optionally back up a `.pfx` locally (already gitignored):

```powershell
$pwd = Read-Host "PFX password" -AsSecureString
Export-PfxCertificate -Cert $cert -FilePath ".\Koli.WinUI.pfx" -Password $pwd
```

#### Build

**Recommended** -- bump the version and publish the MSIX in one step (increments the revision by default, e.g. `1.0.1.3` -> `1.0.1.4`):

```powershell
.\scripts\Publish-Koli.ps1
```

Other examples:

```powershell
# Set an explicit version, then publish
.\scripts\Publish-Koli.ps1 -Version 1.0.2.0

# Bump the build number (3rd segment) instead of the revision
.\scripts\Publish-Koli.ps1 -Bump Build

# Publish without changing the version
.\scripts\Publish-Koli.ps1 -NoBump

# Portable unpackaged folder instead of MSIX
.\scripts\Publish-Koli.ps1 -Unpackaged
```

The canonical version is in `Directory.Build.props` at the repo root (also synced to `Package.appxmanifest`).

Manual publish (no version bump):

```bash
dotnet publish Koli.WinUI/Koli.WinUI.csproj -c Release -r win-x64 -p:WindowsPackageType=MSIX
```

The output is placed in:

```
Koli.WinUI/bin/x64/Release/net8.0-windows10.0.22621.0/win-x64/AppPackages/
└── Koli.WinUI_1.0.1.2_x64_Test/
    ├── Koli.WinUI_1.0.1.2_x64.msix   (~104 MB)
    ├── Koli.WinUI_1.0.1.2_x64.cer    (signing certificate)
    └── Install.ps1                     (automated installer script)
```

Distribute the entire `…_Test/` folder. Users install the certificate once, then double-click the `.msix` — see [Installation](#-installation).

> **Note:** WinUI 3 apps require native Windows App SDK DLLs alongside the executable, so `PublishSingleFile=true` is **not supported**. MSIX is the recommended single-file distribution format.

### Unpackaged folder (portable)

Build a self-contained unpackaged folder for Windows x64:

```bash
dotnet publish Koli.WinUI/Koli.WinUI.csproj -c Release -r win-x64
```

The output is a `publish/` folder containing `Koli.exe` plus all required DLLs (~390 files). Copy the entire folder anywhere and run `Koli.exe` directly.

**Build options reference:**

| Option | Description |
|---|---|
| `-c Release` | Optimised Release build |
| `-r win-x64` | Windows 64-bit runtime identifier |
| `-p:WindowsPackageType=MSIX` | Produce a signed `.msix` installer instead of a loose folder |

---

## 📚 Dependencies

| Package | Version | Purpose |
|---|---|---|
| [`Microsoft.WindowsAppSDK`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK) | 1.6.x | WinUI 3 runtime |
| [`CommunityToolkit.Mvvm`](https://www.nuget.org/packages/CommunityToolkit.Mvvm) | 8.4.x | ViewModels |
| [`CommunityToolkit.WinUI.Controls.SettingsControls`](https://www.nuget.org/packages/CommunityToolkit.WinUI.Controls.SettingsControls) | 8.1.x | Settings UI helpers |
| [`WinUIEx`](https://www.nuget.org/packages/WinUIEx) | 2.3.x | Window/tray helpers |
| [`Microsoft.Extensions.DependencyInjection`](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection) | 8.0.x | DI container |
| [`Microsoft.Windows.Compatibility`](https://www.nuget.org/packages/Microsoft.Windows.Compatibility) | 8.0.x | Windows-specific APIs (DPAPI, etc.) |
| [`NAudio`](https://www.nuget.org/packages/NAudio) | 2.2.1 | WASAPI microphone & loopback capture, WAV header writing |

The OpenAI Realtime WebSocket session is implemented directly on top of `System.Net.WebSockets.ClientWebSocket` — no third-party OpenAI SDK is required.

---

## 🐛 Troubleshooting

### Application does not start

- If you published with `PublishSingleFile=true`, that is the problem: WinUI 3 requires native Windows App SDK DLLs alongside the executable. Use the MSIX build or the unpackaged folder build instead — see [Production Build](#-production-build).
- Ensure `Config/appsettings.json` exists (it is created automatically on first run).
- Check that `ApiKey` and, if applicable, `Endpoint` are correctly set.
- If not using the self-contained build, verify that the .NET 8.0 Runtime is installed.

### No transcription output

- Confirm your microphone is working and not in use by another application.
- Check the status bar for error messages.
- Open **sidebar → Debug** to inspect the raw API request and response.
- Verify the API key is valid and has not exceeded its quota.

### Transcription API errors

- **OpenAI / Azure**: ensure the endpoint format is `https://{resource}.openai.azure.com` and that the specified model is deployed.
- **On-premise**: ensure the endpoint URL does **not** contain `openai.com` and that the server is reachable.
- **Realtime models**: only `api.openai.com` is currently supported for WebSocket Realtime — set `Endpoint` to `""` to use it.
- Check your internet connection and firewall rules.
- **You don't lose the audio**: whenever the transcription call fails in dictation mode, the recording is preserved as a WAV file and shown at the top of the **History** view (see [Pending recordings](#pending-recordings-failed-transcriptions)). Fix the configuration / network, then click **Retry transcription** on the card to retranscribe it without re-recording.

### Audio issues

- Try setting `Device` to `"default"` in `appsettings.json`.
- Verify microphone permissions in **Windows Settings → Privacy → Microphone**.
- If audio is clipped or distorted, confirm `SampleRate` is `16000`.

### System-audio capture (meeting mode) is silent

- WASAPI loopback records the **default playback device**. If you hear the call through headphones but Koli records silence, set the same device as the *Default Output* in Windows sound settings.
- Some exclusive-mode applications (rare DAWs / VST hosts) block loopback. Switch them to shared mode or pick a different output device.

### Auto-typing not working

- Ensure `TypeInActiveWindow` is `true`.
- Click in the target window **before** starting recording so the application captures the target window handle.
- Some applications running as Administrator may block synthetic keyboard input from a normal-privilege process — run Koli as Administrator in that case.

### API key lost or not recognised

- Delete `Config/api.secret` and set `ApiKey` again in `appsettings.json`; it will be re-encrypted on next launch.
- The encrypted secret is bound to the current Windows user account (DPAPI `CurrentUser` scope) and cannot be used on a different machine or user account.

---

## 🗺️ Roadmap

Planned / under consideration:

- Azure OpenAI Realtime WebSocket support.
- Per-meeting custom diarization model selection.
- Live translation in meeting mode.
- macOS / Linux ports (would require replacing WASAPI, DPAPI, and Win32 platform layers).

Suggestions and feature requests are welcome — see [Contributing](#-contributing).

---

## 🤝 Contributing

Contributions are very welcome! To get started:

1. **Fork** the repository and create a feature branch:
   ```bash
   git checkout -b feature/my-improvement
   ```
2. Make your changes following the existing code style and conventions (see [Development](#-development)).
3. Make sure the project builds cleanly with `dotnet build`.
4. Commit with a descriptive message and open a **Pull Request** against `main`.

For bug reports and feature requests, please [open an issue](https://github.com/Aharon-Bensadoun/Koli/issues) with a clear description and steps to reproduce when applicable.

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for the full text.

```
Copyright © 2026 Aharon Bensadoun

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the conditions stated in the LICENSE file.
```

---

## 👤 Author & Contact

**Aharon Bensadoun**

- 📧 Email: [bensadounaharon@gmail.com](mailto:bensadounaharon@gmail.com)
- 🐙 GitHub: [@Aharon-Bensadoun](https://github.com/Aharon-Bensadoun)
- 🔗 Project: [github.com/Aharon-Bensadoun/Koli](https://github.com/Aharon-Bensadoun/Koli)

---

<div align="center">

If you find Koli useful, please ⭐ the repo — it helps a lot!

</div>
