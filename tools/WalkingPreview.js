// SPDX-License-Identifier: Apache-2.0
// Same transform and independent expression timing as PetForm.Expressions.cs.
let state = 'running-right', frame = 0, paused = false, seconds = 0;
const smooth = x => { x = Math.max(0, Math.min(1, x)); return x*x*(3-2*x); };
const cards = [...document.querySelectorAll('canvas')].map(canvas => ({
  canvas, context: canvas.getContext('2d'), images: new Map(),
  blinkAt: .65 + Math.random()*.85, smileAt: 1.6 + Math.random()*1.9,
  smileDuration: 1.3 + Math.random()*.65
}));
function image(card, path) {
  if (!card.images.has(path)) {
    const img = new Image(); img.onload = draw; img.src = card.canvas.dataset.root + path + '?v=foreground-arms-9';
    card.images.set(path, img);
  }
  return card.images.get(path);
}
function expressions(card) {
  const id = card.canvas.dataset.id, duration = id === 'spark' ? .19 : id === 'pippin' ? .26 : .22;
  const bias = id === 'pippin' ? 1.2 : id === 'moss' ? .6 : 0;
  if (seconds > card.blinkAt + duration) card.blinkAt = seconds + 2.5 + bias + Math.random()*2.6;
  if (seconds > card.smileAt + card.smileDuration) {
    card.smileAt = seconds + 4.5 + bias + Math.random()*4.1;
    card.smileDuration = 1.3 + Math.random()*.65;
  }
  const b = (seconds-card.blinkAt)/duration, s = (seconds-card.smileAt)/card.smileDuration;
  const blink = b < 0 || b > 1 ? 0 : b < .42 ? smooth(b/.42) : b < .62 ? 1 : 1-smooth((b-.62)/.38);
  const smile = s < 0 || s > 1 ? 0 : Math.min(smooth(s/.24), smooth((1-s)/.32));
  return [Math.round(blink*3), Math.round(smile*3)];
}
function faceTransform(g, id, phase) {
  const t = phase/24*Math.PI*2;
  if (id === 'pippin' ? state === 'running-right' : state === 'running-left') {
    g.translate(384,0); g.scale(-1,1);
  }
  if (id !== 'pippin') {
    g.translate(Math.sin(t)*.9,Math.cos(t*2)*.75);
    g.translate(192,260); g.rotate(Math.sin(t)*.45*Math.PI/180); g.translate(-192,-260);
  }
  const turtle = id === 'pippin', amount = turtle ? .70 : id === 'moss' ? .82 : id === 'spark' ? 1.08 : 1;
  const [x,y] = turtle ? [151,296+20*(1-Math.exp(-20/20))] : id === 'spark' ? [199,187] : id === 'moss' ? [190,200] : [185,194];
  const targetX = x;
  const targetY = turtle ? y+Math.cos(2*t)*.6 : y;
  g.translate(targetX,targetY); g.rotate(amount*(2.1*Math.sin(t-.38)+.35*Math.sin(2*t-.55))*Math.PI/180);
  if(turtle) {
    g.scale(1+Math.sin(t-.45)*2/143,1);
  }
  g.translate(-x,-y);
}
function draw() {
  const n = ['idle','waiting','failed'].includes(state) ? 8 : 24;
  for (const card of cards) {
    const base = image(card,state+'/'+String(frame%n).padStart(2,'0')+'.png');
    if (!base.complete || !base.naturalWidth) continue;
    const g = card.context; g.clearRect(0,0,384,416); g.drawImage(base,0,0);
    if (state.startsWith('running')) {
      const [blink, smile] = expressions(card);
      g.save(); faceTransform(g, card.canvas.dataset.id, frame%24);
      for (const patch of [blink, smile ? smile+3 : 0]) if (patch) {
        const img = image(card,'profile-expressions/'+String(patch).padStart(2,'0')+'.png');
        if (img.complete && img.naturalWidth) g.drawImage(img,0,0);
      }
      g.restore();
    }
  }
  document.getElementById('counter').textContent = state+' · '+(frame%n+1)+' / '+n+' · 手臂最外层 v9'+(state.startsWith('running')?' · 表情独立变化':' · 站立 / 交互素材');
}
function step() { frame++; seconds += .04; draw(); }
for (const card of cards) {
  for (let f=0;f<24;f++) for (const direction of ['running-left','running-right'])
    image(card,direction+'/'+String(f).padStart(2,'0')+'.png');
  for(let p=1;p<=6;p++) image(card,'profile-expressions/'+String(p).padStart(2,'0')+'.png');
}
setInterval(() => { if(!paused) step(); }, 40); draw();
