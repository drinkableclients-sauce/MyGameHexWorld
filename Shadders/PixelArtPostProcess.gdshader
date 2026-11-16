shader_type canvas_item;

// Parámetros ajustables desde el Inspector o la Toolbar en tiempo real
uniform float pixel_size : hint_range(1.0, 16.0, 1.0) = 3.0;
uniform float color_levels : hint_range(2.0, 64.0, 1.0) = 24.0;
uniform float saturation : hint_range(0.0, 2.0) = 1.25;
uniform float noise_amount : hint_range(0.0, 0.1) = 0.015;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

// Función simple de ruido pseudoaleatorio para dar textura de píxel orgánico
float random(vec2 uv) {
    return fract(sin(dot(uv.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

void fragment() {
    // 1. Pixelación por bloques de tamaño configurable
    vec2 size = vec2(textureSize(SCREEN_TEXTURE, 0));
    vec2 grid_uv = floor(UV * size / pixel_size) * pixel_size / size;
    
    vec4 col = texture(SCREEN_TEXTURE, grid_uv);
    
    // 2. Control de saturación y vitalidad de los colores
    vec3 gray = vec3(dot(col.rgb, vec3(0.299, 0.587, 0.114)));
    col.rgb = mix(gray, col.rgb, saturation);
    
    // 3. Cuantización de color (Reduce los gradientes suaves a bloques de color estilo retro)
    col.rgb = floor(col.rgb * color_levels) / color_levels;
    
    // 4. Ruido sutil de píxel (Dithering orgánico opcional para evitar bandas de color)
    float n = (random(grid_uv) - 0.5) * noise_amount;
    col.rgb += vec3(n);
    
    COLOR = col;
}