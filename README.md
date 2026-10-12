# GrammarApp

Portable, **fully offline** grammar checker + EN⇄ES translator for any text box on Windows (browsers, Office, Slack, Notepad…).

**Use:** select text (or just click inside a field) → `Ctrl+Shift+Space` → click a suggestion or **Fix all** (the live translation shows in the second row; **Use ↑** swaps it in) → `Ctrl+Enter` pastes the result back. `Esc` closes, `Ctrl+Z` undoes.
Left-click the tray icon to type or paste text directly; then the main button copies the result to the clipboard.

**Build** (needs the .NET 8 SDK and internet *once* to fetch the 2.5 GB model):

```
dotnet publish GrammarApp.csproj -c Release -o publish
```

Copy the whole `publish` folder anywhere (USB stick, another PC). It needs no install and no internet; `model.gguf` must stay next to `GrammarApp.exe`.

**Engine:** a local AI model ([Qwen3 4B Instruct 2507](https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507), Q4_K_M, Apache 2.0) running via llama.cpp. It uses **any GPU automatically** (NVIDIA, AMD or Intel, through Vulkan, so no CUDA install is needed) and falls back to the CPU. On the dev PC (Quadro P600) a check takes ~5 s. The tray tooltip shows which device is in use.

**Tests** (xUnit):

```
dotnet test GrammarApp.sln --filter "Category!=Integration"   # fast unit tests, no model needed
dotnet test GrammarApp.sln                                    # + integration tests against the real model (~45 s)
```

For how it works inside (flows, the model, the diff, guards, packaging) see [ARCHITECTURE.md](ARCHITECTURE.md).
