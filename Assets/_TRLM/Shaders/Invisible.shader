Shader "TRLM/Invisible"
{
    // Renders nothing: no color, no depth write. Used to hide specific submeshes (head, torso)
    // of the player's own first-person body without touching the skeleton or breaking skinning
    // on the submeshes that stay visible (arms, legs).
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ColorMask 0
            ZWrite Off
        }
    }
}
