import type { ViewportFrame } from './frame-transport';

const Vertex = `#version 300 es
out vec2 uv;
void main() {
  vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
  uv = vec2(p.x, p.y);
  gl_Position = vec4(p.x * 2.0 - 1.0, 1.0 - p.y * 2.0, 0.0, 1.0);
}`;

const Fragment = `#version 300 es
precision mediump float;
uniform sampler2D frame;
in vec2 uv;
out vec4 colour;
void main() { colour = vec4(texture(frame, uv).rgb, 1.0); }`;

/**
 * Puts frames on a canvas with WebGL2: one texture upload and a triangle. The Worker's bytes are already sRGB, so the
 * texture is plain RGBA8 and nothing here converts. WebGL rather than `putImageData` because of ADR 0003's numbers.
 */
export class FramePainter {
  private readonly gl: WebGL2RenderingContext;
  private width = 0;
  private height = 0;

  /** Returns null where WebGL2 is not available. */
  static create(canvas: HTMLCanvasElement): FramePainter | null {
    const gl = canvas.getContext('webgl2', { alpha: false, antialias: false, depth: false, powerPreference: 'low-power' });
    return gl ? new FramePainter(gl) : null;
  }

  private constructor(gl: WebGL2RenderingContext) {
    this.gl = gl;
    const program = gl.createProgram();
    for (const [type, source] of [
      [gl.VERTEX_SHADER, Vertex],
      [gl.FRAGMENT_SHADER, Fragment]
    ] as const) {
      const shader = gl.createShader(type);
      if (shader) {
        gl.shaderSource(shader, source);
        gl.compileShader(shader);
        gl.attachShader(program, shader);
      }
    }

    gl.linkProgram(program);
    gl.useProgram(program);
    gl.bindTexture(gl.TEXTURE_2D, gl.createTexture());
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
  }

  paint(frame: ViewportFrame): void {
    const { gl } = this;
    if (frame.width !== this.width || frame.height !== this.height) {
      this.width = frame.width;
      this.height = frame.height;
      gl.texStorage2D(gl.TEXTURE_2D, 1, gl.RGBA8, frame.width, frame.height);
    }

    gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, frame.width, frame.height, gl.RGBA, gl.UNSIGNED_BYTE, frame.pixels);
    gl.viewport(0, 0, gl.drawingBufferWidth, gl.drawingBufferHeight);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
  }
}
