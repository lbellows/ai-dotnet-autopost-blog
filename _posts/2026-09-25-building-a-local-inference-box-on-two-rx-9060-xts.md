---
layout: post
title: "Building a Local Inference Box on Two RX 9060 XTs: llama-swap, Open WebUI, ComfyUI and opencode"
date: 2026-09-25 17:33:15 -0400
tags: [local, llama.cpp, llama-swap, rocm, open-webui, comfyui, opencode, gemma, gpt-oss]
author: Liam Bellows
---

My old desktop, a machine I call `main`, is now a headless inference server. It hosts an OpenAI-compatible endpoint that serves ten chat models and an embedder, with Open WebUI in front of it for chat, ComfyUI on the side for video, and opencode on my daily driver as a coding agent pointed at it. This post covers what went into the build: the hardware, the exact model settings that fit 32 GB of VRAM, and the traps that cost me the most time.

Most of the setup was easy. The hard part was a handful of settings that look like they work, pass a health check, and then fail on the first real request.

## The hardware

| Part | What it is |
|---|---|
| Board / CPU | ASUS PRIME Z390-A, i7-9700K (8c/8t) |
| GPUs | 2x Radeon RX 9060 XT 16 GB (RDNA4, `gfx1200`), 32 GB VRAM pooled |
| PCIe | x8/x8 CPU bifurcation, PCIe 3.0 |
| RAM | 32 GB DDR4-2666 (64 GB while ComfyUI was running, more below) |
| OS / stack | Linux Mint 22.3, ROCm 7.2, Docker |
| Storage | Models on a separate 2 TB NVMe at `/mnt/data` |

Some notes on the hardware:

- **`lspci` shows the 9060 XT as an unnamed `[1002:7590]`.** The PCI ID database is older than the card. The GPU is supported; ROCm lists it as `gfx1200`.
- **Empty PCIe slots don't appear in `lspci`.** When I went by sysfs I concluded that only an x2 and an x1 slot were free. `sudo dmidecode -t slot` showed a free CPU-fed x8 slot (`PCIEX16_2`), so the Z390 runs two GPUs at x8/x8 with no motherboard change. The one casualty was my 2.5 GbE card, because the second GPU physically covers its slot.
- **PCIe 3.0 x8 doesn't matter for inference.** The weights live in VRAM, so PCIe only carries the one-time model load and small per-token activations. NVMe read speed limits the load more than the bus does.
- **XMP was off.** The RAM had been running at the JEDEC 2133 fallback for years. I turned on XMP and Resizable BAR in the same BIOS trip. Neither changed tokens per second measurably, which makes sense: decode is limited by VRAM bandwidth.
- **A second GPU adds capacity, not speed.** llama.cpp's default `--split-mode layer` runs the cards one after the other. Models that already fit on one card run at about the same speed. What changed is the context windows and which models fit at all.

## ROCm vs Vulkan: I measured it

Both backends work on `gfx1200` out of the box. I benchmarked them before choosing (Qwen3-14B Q4_K_M, `-ngl 99`):

| Backend | Prompt t/s | Generation t/s |
|---|---|---|
| ROCm (HIP) | 1290 | **30.8** |
| Vulkan (RADV) | 961 | 18.6 |

ROCm generates about 66% faster, so the stack runs on ROCm. I keep a Vulkan image around as a fallback in case a ROCm update regresses.

## The stack: llama-swap + llama.cpp + Open WebUI

llama-swap is a small Go proxy. It exposes one OpenAI-compatible `/v1` endpoint and starts and stops a `llama-server` process per model on demand. Open WebUI is one client of that endpoint.

**Pitfall: llama-swap has no ROCm image.** Upstream publishes CPU, CUDA, Vulkan, Intel and MUSA images only. Because llama-swap is a single binary, a two-stage Dockerfile copies it onto the official llama.cpp ROCm server image:

```dockerfile
FROM ghcr.io/mostlygeek/llama-swap:cpu AS swap

FROM ghcr.io/ggml-org/llama.cpp:server-rocm
COPY --from=swap /app/llama-swap /app/llama-swap
ENTRYPOINT ["/app/llama-swap"]
CMD ["-config", "/app/config.yaml", "-watch-config", "-listen", ":8080"]
```

When you update, bump both upstream tags together.

The llama-swap half of the compose file:

```yaml
services:
  llama-swap:
    build: .
    ports: ["8080:8080"]
    devices: [/dev/kfd, /dev/dri]
    group_add: ["44", "992"]   # video, render: HOST gids
    security_opt: [seccomp:unconfined]
    ipc: host
    volumes:
      - /mnt/data/ai/models:/models:ro
      - ./config.yaml:/app/config.yaml:ro
```

`group_add` takes the **host's** numeric gids for `video` and `render`. Group names are resolved inside the container, where they may not exist or may have different numbers.

**Pitfall: Docker's data root.** The ROCm base image is about 23 GB, and BuildKit unpacks it again into its own snapshot store. On a 98 GB root partition that failed with ENOSPC. I moved Docker's `data-root` to the 2 TB data drive.

### The shared server flags

Every model starts from one macro:

```yaml
macros:
  "server": >
    /app/llama-server --host 127.0.0.1 --port ${PORT}
    --no-webui --jinja -ngl 99 -fa on -np 2 -kvu
```

Two of those flags come from hard lessons:

- **`-np 2`**: llama-server defaults to 4 parallel slots, and **each slot allocates its own compute buffers.** On one 16 GB card, `gemma-26b` crashed at 32K context with 4 slots, ran fine with 2, and reached 49K with 1. Of all the settings, slot count had the biggest effect on memory. Two slots cover one person chatting while Open WebUI generates a title in the background.
- **`-kvu`** (`--kv-unified`): **setting `-np 2` explicitly silently halved every context window.** llama.cpp splits `-c` across slots unless the KV cache is unified. Unified KV is on by default only when the slot count is automatic, so setting the count yourself turns it off. For days, a model configured at 32K served 16K per request, and clients got `400 exceeds the available context size (16384 tokens)`. Adding `-kvu` restored the full window at no VRAM cost, because the total KV pool is the same. To check what a model actually serves, run `curl http://main:8080/upstream/<model>/props | jq .default_generation_settings.n_ctx`.

### Sizing context: a model that loads isn't a model that runs

This rule saved me more time than any other: **never size a context from a successful load.** Loading allocates the weights and the KV cache. Compute buffers are allocated on the *first request*. `gemma-26b` passed its health check at 98% VRAM and then died mid-completion with `upstream process exited unexpectedly`.

I wrote a probe script that walks up a context ladder, and at each rung it:

1. unloads everything first (a model still inside its TTL holds VRAM and makes every rung look like a failure),
2. loads the embedder, because production always has room for it,
3. loads the chat model at the candidate context,
4. sends a **real completion** that nearly fills the window,
5. records peak VRAM across both cards.

Before probing anything, **read the trained context from the GGUF header.** Three of my models were already at their trained ceiling on one card, so the second GPU couldn't give them more.

## The models, and the settings that fit 32 GB VRAM

All chat models sit in one **exclusive** llama-swap group, because only one fits at a time. The embedder sits in a separate non-exclusive group so it loads *alongside* the chat model instead of evicting it. tok/s is the **median decode speed from real chat traffic** on the two-card box, not a `llama-bench` number.

| Model id | Weights | Context | KV cache | Reasoning | tok/s |
|---|---|---|---|---|---|
| `gpt-oss-20b` | gpt-oss 20B, MXFP4 (native) | 131072 | **f16** | effort `low` | 75 |
| `gemma-26b` | Gemma 4 26B-A4B, Google QAT q4_0 + vision | 262144 | q8_0 | `--reasoning off` | 42 |
| `gemma-26b-think` | same weights | 262144 | q8_0 | on, budget 1536 | 41 |
| `ornith-35b` | Ornith 1.5 35B-A3B, Q4_K_M | 262144 | **f16** | on, budget 1024 | 42 |
| `gemma-12b` | Gemma 4 12B, QAT q4_0 + vision | 262144 | f16 | `--reasoning off` | 32 |
| `cwc-nemo-12b` | Mistral-Nemo 12B fine-tune, Q4_K_M | 131072 | q8_0 | none | 31 |
| `qwen38-27b` | Qwen3.8 27B, UD-IQ4_XS | 131072 | q8_0 | `xhigh`, budget 1024 | 15 |
| `gemma-31b` | Gemma 4 31B dense, QAT q4_0 + vision | 98304 | q8_0 | on, budget 1024 | 13 |
| `qwen3-embed` | Qwen3-Embedding 0.6B, Q8_0 | 2048 | n/a | encoder | n/a |

I also run abliterated ("uncensored") versions of `gemma-26b` and `qwen38-27b` with the same flags.

Why each setting:

- **`gpt-oss-20b` is still the fastest model on the box.** It's the oldest model here and I nearly dropped it for its age without benchmarking it. A small-active-parameter MoE beats newer dense models on this hardware every time. Keep its **KV cache at f16**: gpt-oss uses attention sinks and doesn't tolerate a quantized cache. Reasoning effort is `low`: at temperature 0, `low` and `high` got the same score on my task set and failed the *same* task, but `high` used 6x the tokens.
- **Gemma 4 holds 256K cheaply** because it uses sliding-window attention: most layers don't grow their KV cache with context. On `gemma-26b`, going from 64K to 256K cost about 5 GB of VRAM (18.3 GB to 23.4 GB). Full-attention models behave very differently. `cwc-nemo-12b` (Mistral-Nemo) costs 160 KiB per token at f16, and on one card it topped out at 45K with f16 KV versus 81K with q8_0.
- **`ornith-35b` gets the full 256K with an unquantized KV cache.** Only about 10 of its 41 layers keep a real KV cache; the rest use linear attention. That comes to about 20 KiB per token, or about 5 GB for the whole window. It runs 3x faster than `qwen38-27b` with twice the context, and it scored best on my task set. It's my default for agentic coding.
- **`qwen38-27b` is capped at 128K on purpose.** 256K runs, but at 87% VRAM, and prompt processing drops with context: about 890 t/s at 16K, 150 to 350 at 32K, 110 to 290 at 64K. At about 150 t/s, filling a 256K window takes about **29 minutes before the first token**. I'd rather have the margin than a window I'd never actually fill.
- **`gemma-31b` is at 98304** because 131072 runs at 93% VRAM, too tight to keep, and 262144 won't load.
- **Don't "upgrade" the QAT files.** Google's Gemma QAT weights were trained *for* q4_0, so a generic Q4_K_M of the same model is a downgrade at the same size. MXFP4 is likewise gpt-oss's native release format.
- **The embedder stays at `-c 2048`.** At the default 8192 it held about 2 GB, and on one card `gemma-26b` couldn't load at *any* context next to it. Open WebUI chunks at about 250 tokens anyway. It unloads after 10 idle minutes (`ttl: 600`), and I size every chat model *with* the embedder resident, so there's always room for it to load back.
- **Vision needs the separate `--mmproj` projector file.** The projectors differ between models: the 12B's is 175 MB and also includes an audio encoder, while the 26B's is a 1.2 GB SigLIP tower. They're not interchangeable.

## Reasoning settings that don't do what they say

This section cost the most time, and the problem was the same every time: a setting that parses isn't necessarily a setting that works.

**`enable_thinking: false` doesn't stop Gemma 4 26B from thinking.** On a plain "write a 300-word summary" request, the stock model returned `content: ""`, 13,390 characters of `reasoning_content`, and `finish_reason: length`. It spent the whole token budget thinking and never answered. The chat template is byte-identical to Google's, so the template isn't at fault, and llama.cpp passes `enable_thinking=true` itself, which overrides the template default. The fix is the **server flag `--reasoning off`**. On IFEval, the score went from 0.444 to 0.9128 and wall time from about 70 minutes to 9.5, which is now the best instruction-following score on the box. Gemma had never been bad at following instructions; the benchmark was scoring empty responses.

**Unlimited thinking produces empty answers, so cap it.** For the models that should think, I tried three settings: unlimited, off, and bounded. **Bounded won on every model, and it was usually also faster:**

```
--reasoning on
--reasoning-budget 1024
--reasoning-budget-message "I have thought enough. I will now give my final answer."
```

The budget message is what makes the model stop reasoning and answer, so keep it. `gemma-31b` went from 17/19 to 19/19 *and* from 730 s to 593 s. One catch: a request with `max_tokens` below the budget plus the answer still comes back empty, because the request runs out of tokens before the budget kicks in.

**Qwen3.8's template silently maps `high` to `xhigh`**, and it defaults to `xhigh` if you pass nothing. gpt-oss's template doesn't validate the value at all: a typo is pasted into the system message as "Reasoning: <typo>" without any error.

**gpt-oss returned blank replies about 35% of the time on questions it couldn't answer.** It was calling its built-in `browser` and `repo_browser` tools, which don't exist here, by emitting `commentary to=search`. llama.cpp's harmony parser found no matching tool and **dropped the message**. The client got `content: ""`, no tool calls, and `finish_reason: stop`. One sign of this is the usage count: about 40 more completion tokens than the visible text accounts for. The fix goes in `model_identity` (a chat-template kwarg that lands in the *system* message, where gpt-oss decides which tools it has): a rule that tools exist only when a `# Tools` section is present. That brought blank replies down to about 5%, and real tool calling still worked 6/6. Putting the same words in Open WebUI's system prompt didn't help, because that lands in the developer message. Upgrading llama.cpp didn't help either.

**Only gpt-oss knows today's date.** Its template injects `Current date:`; the others don't, so they confidently date things by their training cutoff. `qwen38-27b` was saving Open WebUI memories as April 2026 in September. Open WebUI's `{{CURRENT_DATE}}` and `{{CURRENT_WEEKDAY}}` variables in the system prompt fixed it.

## llama-swap operational traps

- **It hot-reloads `config.yaml`.** It polls every 2 s. You don't need to restart the container after an edit, but **don't edit while a request is in flight**: a reload in the middle of a request can leave that request hanging forever.
- **A reload that fails keeps the old config running.** I deleted a model block but left its id in the group member list. llama-swap logged a `WARN`, kept serving the *previous* config, and my test then looked like the fix had failed. After any edit, check `/v1/models`.
- **Never edit the config with `sed -i`, `mv`, or an editor that saves by renaming.** A single-file bind mount pins the *inode*, so the container keeps reading the old, deleted file, and it still logs `configuration reloaded`. Write the file in place, or run `docker restart llama-swap`.
- **`/unload` is a GET.** A POST returns 405.
- **`llama-bench` reports `failed to load model` when it really means it couldn't allocate a ROCm buffer.** Stop llama-swap before benchmarking, or you'll think the GPU architecture is unsupported when the GPU is just busy.

## Open WebUI

The Open WebUI half of the compose file is short:

```yaml
  open-webui:
    image: ghcr.io/open-webui/open-webui:main
    ports: ["80:8080"]
    environment:
      - OPENAI_API_BASE_URL=http://llama-swap:8080/v1
      - OPENAI_API_KEY=local
      - WEBUI_AUTH=false
      - ENABLE_OLLAMA_API=false
      - RAG_EMBEDDING_ENGINE=openai          # use qwen3-embed, not a bundled model
      - RAG_EMBEDDING_MODEL=qwen3-embed
      - RAG_OPENAI_API_BASE_URL=http://llama-swap:8080/v1
```

Things I got wrong at first:

- **Most settings live in the database, not in environment variables.** `DEFAULT_MODELS`, prompt suggestions, follow-up generation and similar variables only *seed* the config table on the first run. After that, changing the compose file does nothing. To change them later, use the admin UI or edit `webui.db` and restart.
- **System prompts only exist in Open WebUI.** llama-server dropped `--system-prompt-file`, and llama-swap never touches message bodies, so a client that calls `:8080` directly gets no system prompt. I keep the prompts as files (`base.md` plus one file per model) and a script pushes them into Open WebUI's model table.
- **Vision needs two settings:** `--mmproj` on the server **and** the model's vision capability turned on in Open WebUI. Without the second, the upload button stays hidden even though the model can read images.
- **`curl http://main/api/models` returns 401 even with `WEBUI_AUTH=false`.** That's Open WebUI requiring a session cookie for its own API, not a broken connection to llama-swap. Test from inside the container instead.
- **The Arena model can force a model load on every turn.** It sends each turn to two models for blind voting, and in an exclusive group that means swapping. I turned it off.

## ComfyUI (and why it needed 64 GB of RAM)

For video I ran ComfyUI in a **native venv, not Docker** (torch 2.14.0 built for ROCm 7.2, matching the host's ROCm), because it changes fast and depends on custom nodes. It was never a service: ComfyUI keeps models cached in VRAM between renders and would starve llama-swap, so I started it by hand when I needed it.

**The worst pitfall: `PYTORCH_HIP_ALLOC_CONF=expandable_segments:True`.** I set it during install to reduce fragmentation. On `gfx1200` it caused amdgpu page faults (`[gfxhub] page fault ... PERMISSION_FAULTS: 0x5`). Depending on another flag, that showed up either as a `hipErrorIllegalAddress` exception or as a *silent hang* with the GPU at 100%, which looked like slowness. I spent runs on attention flags and suspected the text encoder before I removed the variable. After that, everything worked on the first try, with no torch downgrade. The catch is that **every PyTorch OOM message recommends exactly that setting.** On this GPU it turns an OOM into a page-fault storm.

Settings that did work, measured on a single 16 GB card:

- `TORCH_ROCM_AOTRITON_ENABLE_EXPERIMENTAL=1`: without it, SDPA attention on RDNA4 silently falls back to the math backend, which is much slower and uses far more VRAM.
- `--reserve-vram 4`: this forces ComfyUI to offload the diffusion model *before* the VAE decode allocates memory, instead of OOMing first and offloading afterward.
- **Model: Wan 2.2 T2V A14B as Q4_K_M GGUF** (two 9.65 GB stages that load one after the other), with the lightx2v 4-step LoRAs, euler / cfg 1.0 / shift 5.0 / 2+2 steps, and the Wan **2.1** VAE. 704x480, 81 frames (5 s at 16 fps) renders in **201 s**. It looked much better than the 5B model at any resolution. The improvement comes from the bigger model, not from GGUF: quantization is simply what made 14B fit.
- **Don't tile the VAE decode with the 2.1 VAE.** A periodic color shift and pixelation appeared every ~12 frames, which lined up with the temporal tile seams. I had copied the tiling setting from the 5B model, whose larger VAE really did run out of memory. The A14B path uses a 242 MB VAE that decodes 81 frames in 18 s untiled.
- **The ComfyUI-GGUF node has no `models/unet_gguf` directory**, even though a folder key with that name exists. Put the GGUF files in `models/unet/`, or the model dropdown will be empty without any error.
- **Pin torch when updating.** ComfyUI's `requirements.txt` lists torch without a version. If it ever adds a version floor, pip will replace your ROCm build with a CUDA wheel from PyPI. My update script pins torch from `pip freeze` and then checks that `"rocm" in torch.__version__`.

**RAM was the real constraint.** Idle ComfyUI with an empty queue held about **26 GB of resident memory**. The LLM stack needs about 3.5 GB of host RAM, since it's VRAM-bound and its peak with the largest model loaded is 3.5 GB of 31. On 32 GB, 26 + 3.5 pushes the machine straight into swap. That's why I upgraded to **64 GB (4x16 GB DDR4)**. There was some risk that 8 ranks on Z390's daisy-chain memory layout would fall back to 2133, but it held 2666 on all four DIMMs. Handing the GPU back and forth still took a script in each direction: one POSTs ComfyUI's `/free`, the other unloads llama-swap. Neither side releases VRAM on its own, and llama-swap has no per-model hook where you could attach that.

The machine is back on 32 GB now, so ComfyUI is parked. The handoff scripts refuse to run and print the RAM shortfall instead of starting a swap storm. **Two 16 GB GPUs hold a 14B video model easily; 32 GB of system RAM doesn't hold ComfyUI next to anything else.** If you're planning a ComfyUI box, budget 64 GB of RAM before a second GPU.

## opencode from my daily driver

My laptop runs opencode with a provider pointed at `main`:

```json
"llama.cpp": {
  "npm": "@ai-sdk/openai-compatible",
  "name": "llama-swap (main)",
  "options": { "baseURL": "http://main:8080/v1", "apiKey": "none" },
  "models": {
    "ornith-35b": {
      "reasoning": true, "tool_call": true, "attachment": false,
      "limit": { "context": 262144, "output": 32768 }
    },
    "gemma-26b": {
      "reasoning": false, "tool_call": true, "attachment": true,
      "modalities": { "input": ["text", "image"], "output": ["text"] },
      "limit": { "context": 262144, "output": 32768 }
    }
  }
}
```

with `"model": "llama.cpp/ornith-35b"` as the default. The same pattern covers all the models. Two things to know:

1. **Every client has to know each model's context window itself.** llama-swap's `/v1/models` doesn't report context limits, so a client that relies on discovery assumes a default and only finds out mid-turn with a 400 error. Take the values from `/upstream/<model>/props`, not from notes written before a config change. When the second GPU arrived, my client tables were 4 to 8x too small.
2. **Point `small_model` at a remote provider.** Every agent harness splits off a cheap model for titles and summaries (opencode's `small_model`, aider's `--weak-model`) and assumes it can call both models at once. With an exclusive group, **every turn pays two full model loads.** I measured about 28 s per request, of which about 5 s was generation. The sign in `docker logs llama-swap` is a strict alternation of "Health check passed" between two models. My `small_model` now runs on a hosted flash model, so it never competes for VRAM.

I checked tool calling with a two-round test: the model has to emit the call and then use the result. Every chat model passed. One oddity: the Mistral-Nemo template returns **HTTP 400 unless tool-call IDs are exactly 9 alphanumeric characters**, so anything that replays tool IDs by hand has to follow that rule.

## Measuring without fooling yourself

I published at least three wrong conclusions before adopting these rules:

- **Don't tune a stochastic bug at temperature 0.** Greedy decoding hid gpt-oss's blank-reply bug completely, which made every prompt change look like a fix.
- **Measure on the kind of input you're trying to fix.** The blank-reply rate depended on the question type: 30% on "read this file on the machine" questions, 0% on arithmetic. A mixed test set measured the mix, not the change.
- **Alternate both arms in one session, with n ≥ 20 per cell.** The baseline drifted from 1/6 to 5/6 between runs with an identical config.
- **The benchmark has to be the only client.** A second client asking for a different model makes every request trigger a full model load, which looks exactly like a 100x slowdown, while `rocm-smi` shows both GPUs at 99%.
- **Suspect the test harness when two models fail identically.** My vision test "proved" that both Gemmas couldn't read `KESTREL-4820`. The canvas was too narrow and PIL had clipped the last character.

## Summary

- On RDNA4, use ROCm over Vulkan (+66% generation), and copy llama-swap's binary onto the llama.cpp ROCm image.
- Use `-np 2 -kvu`. Size every context with a real completion, with the embedder loaded.
- Use sliding-window and hybrid-linear models (Gemma 4, Ornith 1.5) to get 256K context on 32 GB of VRAM. Full-attention models need a q8_0 KV cache.
- Bound thinking with a budget and a budget message. Turn it off with `--reasoning off`, not a template kwarg.
- Use one chat model per client. A second model on the same machine means a model load on every turn.
- ComfyUI needs system RAM more than VRAM. Plan for 64 GB.
