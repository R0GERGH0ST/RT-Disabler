# RT Freedom — Game Compatibility Report

**Generated**: Manual analysis  
**Tool Version**: 1.0.0  

---

## System Requirements Summary

| Feature | Minimum | Recommended |
|---------|---------|-------------|
| OS | Windows 10 64-bit | Windows 11 64-bit |
| Runtime | .NET 9.0 | .NET 9.0 |
| GPU (analysis) | Any DirectX 12 GPU | NVIDIA GTX 1060+ / AMD RX 580+ |
| GPU (RT games) | RTX 2060 / RX 6600 | RTX 3070+ / RX 6800+ |

---

## Game Analysis Reports

### 1. Indiana Jones and the Great Circle

| Property | Value |
|----------|-------|
| **Engine** | id Tech 7 (modified) |
| **Rendering API** | Vulkan |
| **RT Extensions** | VK_KHR_ray_tracing_pipeline, VK_KHR_acceleration_structure |
| **Mandatory RT** | Yes — Global Illumination |
| **Optional RT** | Reflections, Shadows |
| **Non-RT Fallback** | ❌ No rasterized GI path detected |
| **Compatibility** | ❌ Incompatible with non-RT GPUs |

#### Analysis Details

The game uses id Tech 7 with significant modifications for Vulkan-based ray tracing. The Global Illumination system is built entirely on hardware-accelerated ray tracing using Vulkan RT extensions.

**Key findings:**
- The renderer initializes RT acceleration structures as part of its mandatory startup pipeline
- GI probes and light transport are computed via ray-tracing dispatch calls
- No fallback rasterized GI path (e.g., light probes, screen-space GI) has been identified
- Disabling RT settings via config files causes rendering artifacts or crashes
- The game checks for RT hardware support at launch and refuses to start without it

**What RT Freedom can do:**
- ✅ Reduce RT quality settings (ray count, bounce count, resolution) for low-end RT GPUs
- ✅ Analyze installed version and report compatibility
- ❌ Cannot bypass the mandatory RT requirement
- ❌ Cannot provide a rasterized fallback

**Recommendation:** This game requires a GPU with hardware ray-tracing support. Minimum: NVIDIA RTX 2060 or AMD RX 6600 XT.

---

### 2. Gears 5

| Property | Value |
|----------|-------|
| **Engine** | Unreal Engine 4 |
| **Rendering API** | DirectX 12 |
| **RT Extensions** | DXR 1.0 (optional) |
| **Mandatory RT** | No |
| **Optional RT** | Reflections |
| **Non-RT Fallback** | ✅ Full rasterized renderer |
| **Compatibility** | ✅ Fully Compatible |

#### Analysis Details

Gears 5 uses Unreal Engine 4 with DirectX 12. Ray-traced reflections were added as an optional visual enhancement and are not required for the game to run.

**Key findings:**
- The game has a complete screen-space reflections (SSR) fallback
- RT can be toggled off in the game's graphics settings
- DXR functions are loaded conditionally based on hardware support
- The game runs correctly on GPUs without any RT support
- No mandatory RT acceleration structure initialization

**What RT Freedom can do:**
- ✅ Verify RT is properly disabled in configuration
- ✅ Patch configuration files to force rasterized rendering
- ✅ Confirm game version compatibility
- ✅ Full backup and restore of configuration

**Recommendation:** No special action needed. The game supports non-RT GPUs natively. RT Freedom can ensure RT settings are properly disabled.

---

### 3. DOOM: The Dark Ages

| Property | Value |
|----------|-------|
| **Engine** | id Tech 8 |
| **Rendering API** | Vulkan |
| **RT Extensions** | VK_KHR_ray_tracing_pipeline, VK_KHR_acceleration_structure, VK_KHR_ray_query |
| **Mandatory RT** | Yes — Entire lighting pipeline |
| **Optional RT** | None (all RT is mandatory) |
| **Non-RT Fallback** | ❌ No rasterized renderer |
| **Compatibility** | ❌ Incompatible with non-RT GPUs |

#### Analysis Details

DOOM: The Dark Ages represents a fundamental shift from DOOM Eternal's traditional rasterized renderer. The id Tech 8 engine uses a path-tracing based rendering pipeline where all lighting, shadows, and global illumination are computed through ray tracing.

**Key findings:**
- The entire lighting pipeline is path-traced — this is not an optional enhancement
- Unlike DOOM Eternal, there is no traditional rasterization rendering path
- All light transport (direct, indirect, ambient occlusion) uses RT dispatches
- The engine requires RT acceleration structure support at initialization
- Config modifications cannot restore a rasterized path because one does not exist in the engine
- The game validates RT hardware support and will not launch without it

**What RT Freedom can do:**
- ✅ Reduce RT quality settings (path tracing samples, bounce limits, resolution scale)
- ✅ Analyze installed version and GPU compatibility
- ❌ Cannot bypass the mandatory RT requirement
- ❌ Cannot create a rasterized fallback — the engine doesn't have one
- ❌ This is not a configuration problem — it's an architecture decision

**Recommendation:** This game requires a GPU with hardware ray-tracing support. Minimum: NVIDIA RTX 2060 or AMD RX 6600 XT. For playable performance: RTX 3070+ or RX 6800+.

---

## GPU Compatibility Matrix

| GPU Family | RT Support | Tier | Indiana Jones | Gears 5 | DOOM: Dark Ages |
|-----------|-----------|------|---------------|---------|-----------------|
| NVIDIA GTX 10xx | ❌ None | 0 | ❌ | ✅ | ❌ |
| NVIDIA GTX 16xx | ❌ None | 0 | ❌ | ✅ | ❌ |
| NVIDIA RTX 20xx | ✅ DXR 1.0 | 1 | ⚠️ Low settings | ✅ | ⚠️ Low settings |
| NVIDIA RTX 30xx | ✅ DXR 1.1 | 2 | ✅ Medium+ | ✅ | ✅ Medium |
| NVIDIA RTX 40xx | ✅ DXR 1.1 + SER | 3 | ✅ High | ✅ | ✅ High |
| AMD RX 5xxx | ❌ None | 0 | ❌ | ✅ | ❌ |
| AMD RX 6xxx | ✅ DXR 1.0 | 1 | ⚠️ Low settings | ✅ | ⚠️ Low settings |
| AMD RX 7xxx | ✅ DXR 1.1 | 2 | ✅ Medium | ✅ | ✅ Medium |
| Intel Arc A-series | ✅ DXR 1.1 | 2 | ⚠️ Variable | ✅ | ⚠️ Variable |

**Legend:** ✅ Compatible | ⚠️ Partially Compatible (reduced settings) | ❌ Incompatible

---

## Conclusion

Of the three target games:
- **Gears 5** is fully compatible with non-RT GPUs — RT Freedom can help ensure optimal configuration
- **Indiana Jones** and **DOOM: The Dark Ages** have mandatory RT dependencies that cannot be bypassed through software alone
- For mandatory-RT games, RT Freedom provides value by optimizing RT quality settings for lower-end RT GPUs

> **Honesty Notice:** RT Freedom reports actual compatibility rather than promising a universal bypass. Games built with mandatory ray tracing at the engine level cannot be made to run on non-RT hardware through configuration changes alone. This is a fundamental engineering limitation, not a software deficiency.
