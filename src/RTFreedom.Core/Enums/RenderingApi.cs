namespace RTFreedom.Core.Enums;

/// <summary>
/// Specifies the graphics rendering API used by a game.
/// </summary>
public enum RenderingApi
{
    /// <summary>
    /// Unknown or undetected graphics API.
    /// </summary>
    Unknown,

    /// <summary>
    /// Direct3D 11 API.
    /// </summary>
    DirectX11,

    /// <summary>
    /// Direct3D 12 API.
    /// </summary>
    DirectX12,

    /// <summary>
    /// Vulkan API.
    /// </summary>
    Vulkan,

    /// <summary>
    /// OpenGL API.
    /// </summary>
    OpenGL
}
