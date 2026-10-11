namespace RTFreedom.Core.Enums;

/// <summary>
/// Defines the category of patching action used to modify or configure the game.
/// </summary>
public enum PatchType
{
    /// <summary>
    /// Configuration file tweak or parameter change.
    /// </summary>
    ConfigModification,

    /// <summary>
    /// Replacement or injection of compiled or precompiled shaders.
    /// </summary>
    ShaderReplacement,

    /// <summary>
    /// Direct dynamic library redirection or proxy DLL placement.
    /// </summary>
    DllRedirect,

    /// <summary>
    /// Forcing alternative renderer backend (e.g., DX11 fallback or Vulkan wrapper).
    /// </summary>
    RendererSwitch
}
