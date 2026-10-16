// A mock WebXR device (navigator.xr, XRWebGLLayer, a 60 Hz frame loop, a head at 0,1.7,0, two eyes 64 mm apart, two controllers).
// Copied from AubsCraft.Viewer.Tests/ViewerXrTests.cs (Geordi), with the matrix and inverse a real XRRigidTransform has (LostSpawns reads them); injected before the page loads. Each frame is mirrored onto #xr-mirror.
(() => {
  const xr = window.__xr = { jitter: 0, left: [0, 0], right: [0, 0], leftY: false, rightB: false, x: false, trigger: false, leftTrigger: false, leftThumb: false, rightThumb: false, rightA: false, frames: 0, ended: false, mode: null, blend: 'opaque',
    rayRight: { p: [0.25, 1.3, -0.2], q: [0, 0, 0, 1] }, rayLeft: { p: [-0.25, 1.3, -0.2], q: [-0.7071068, 0, 0, 0.7071068] } };
  // The right controller points from where it is at a point of the room (the left one points at the floor)
  xr.aimAt = (tx, ty, tz) => {
    const p = xr.rayRight.p; let dx = tx - p[0], dy = ty - p[1], dz = tz - p[2];
    const len = Math.hypot(dx, dy, dz); dx /= len; dy /= len; dz /= len;
    // the rotation taking (0,0,-1) to d: axis = (0,0,-1) x d = (dy, -dx, 0), angle = acos(-dz)
    const angle = Math.acos(Math.max(-1, Math.min(1, -dz))); const ax = dy, ay = -dx; const al = Math.hypot(ax, ay) || 1;
    const s = Math.sin(angle / 2);
    xr.rayRight.q = [ax / al * s, ay / al * s, 0, Math.cos(angle / 2)];
  };
  // A rigid transform the way a real XRRigidTransform has it: position, orientation, the 4x4 matrix (column-major) and the inverse transform
  const T = (p, q) => {
    const { x, y, z, w } = q; const xx = x * x, yy = y * y, zz = z * z;
    const m = new Float32Array([1 - 2 * (yy + zz), 2 * (x * y + z * w), 2 * (x * z - y * w), 0,
                                2 * (x * y - z * w), 1 - 2 * (xx + zz), 2 * (y * z + x * w), 0,
                                2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (xx + yy), 0,
                                p.x, p.y, p.z, 1]);
    const i = new Float32Array([m[0], m[4], m[8], 0, m[1], m[5], m[9], 0, m[2], m[6], m[10], 0,
                                -(m[0] * p.x + m[1] * p.y + m[2] * p.z), -(m[4] * p.x + m[5] * p.y + m[6] * p.z), -(m[8] * p.x + m[9] * p.y + m[10] * p.z), 1]);
    return { position: p, orientation: q, matrix: m, inverse: { position: { x: i[12], y: i[13], z: i[14], w: 1 }, orientation: { x: -x, y: -y, z: -z, w: w }, matrix: i } };
  };
  const f = 1 / Math.tan(Math.PI / 4); // 90 degree vertical field of view, square eyes
  const near = 0.05, far = 3000;
  const proj = new Float32Array([f,0,0,0, 0,f,0,0, 0,0,-(far+near)/(far-near),-1, 0,0,-2*far*near/(far-near),0]);
  class Layer {
    constructor(session, gl) {
      this.session = session; this.gl = gl;
      gl.canvas.width = 2048; gl.canvas.height = 1024;
      this.framebuffer = null; this.framebufferWidth = 2048; this.framebufferHeight = 1024;
      this.antialias = false; this.ignoreDepthValues = false;
    }
    getViewport(view) { return view.eye === 'left' ? { x: 0, y: 0, width: 1024, height: 1024 } : { x: 1024, y: 0, width: 1024, height: 1024 }; }
  }
  window.XRWebGLLayer = Layer;
  const mirror = () => {
    let m = document.getElementById('xr-mirror');
    if (!m) {
      m = document.createElement('canvas'); m.id = 'xr-mirror';
      m.style.cssText = 'position:fixed;left:0;top:0;width:1024px;height:512px;z-index:99999;background:#f0f';
      document.body.appendChild(m);
    }
    m.style.display = 'block';
    const gl = xr.glCanvas;
    if (gl && gl.width) { m.width = gl.width; m.height = gl.height; m.getContext('2d').drawImage(gl, 0, 0); }
  };
  const wob = () => { const t = xr.frames * 0.37; return { x: xr.jitter * Math.cos(t * 1.3), y: xr.jitter * Math.sin(t), z: xr.jitter * Math.sin(t * 0.7) }; };
  const pose = (x, eye) => { const w = wob(); return { eye, projectionMatrix: proj, transform: T({ x: x + w.x, y: 1.7 + w.y, z: w.z, w: 1 }, { x: 0, y: 0, z: 0, w: 1 }), isFirstPersonObserver: false }; };
  const makeSession = (mode) => {
    const s = new EventTarget();
    xr.mode = mode; xr.ended = false;
    xr.leave = () => s.end();
    s.environmentBlendMode = mode === 'immersive-ar' ? 'alpha-blend' : 'opaque';
    xr.blend = s.environmentBlendMode;
    s.renderState = {};
    s.requestReferenceSpace = async () => ({});
    s.updateRenderState = (init) => { Object.assign(s.renderState, init); if (init.baseLayer) xr.glCanvas = init.baseLayer.gl.canvas; };
    let handle = 0, timer = 0;
    s.requestAnimationFrame = (cb) => { handle++; timer = setTimeout(() => {
      if (xr.ended) return;
      xr.frames++;
      const frame = {
        session: s,
        getPose: (space) => { const r = space.hand === 'left' ? xr.rayLeft : xr.rayRight; return { transform: T({ x: r.p[0], y: r.p[1], z: r.p[2], w: 1 }, { x: r.q[0], y: r.q[1], z: r.q[2], w: r.q[3] }) }; },
        getViewerPose: () => { const w = wob(); return { transform: T({ x: w.x, y: 1.7 + w.y, z: w.z, w: 1 }, { x: 0, y: 0, z: 0, w: 1 }), views: [pose(-0.032, 'left'), pose(0.032, 'right')] }; },
      };
      cb(performance.now(), frame);
      mirror();
    }, 16); return handle; };
    const pad = (stick, down) => ({ axes: [0, 0, stick[0], stick[1]], buttons: [0,1,2,3,4,5].map(i => ({ pressed: !!down[i], touched: false, value: 0 })) });
    Object.defineProperty(s, 'inputSources', { get: () => [
      { handedness: 'left', targetRaySpace: { hand: 'left' }, gamepad: pad(xr.left, { 4: xr.x, 5: xr.leftY, 0: xr.leftTrigger, 3: xr.leftThumb }) },
      { handedness: 'right', targetRaySpace: { hand: 'right' }, gamepad: pad(xr.right, { 5: xr.rightB, 0: xr.trigger, 3: xr.rightThumb, 4: xr.rightA }) },
    ] });
    s.end = async () => { xr.ended = true; clearTimeout(timer); s.dispatchEvent(new Event('end')); };
    return s;
  };
  Object.defineProperty(navigator, 'xr', { configurable: true, value: {
    isSessionSupported: async () => true,
    requestSession: async (mode) => makeSession(mode),
    addEventListener() {}, removeEventListener() {},
  } });
})();
