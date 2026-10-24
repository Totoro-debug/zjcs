// Isolated development converter. The original Spine runtime evaluates each
// sample; playback remains sampled, rather than a continuous Spine evaluator.
import fs from 'node:fs';
import * as S from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/index.js';
import {BinaryInput} from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/SkeletonBinary.js';
const jobs = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const result = {};
for (const [id, job] of Object.entries(jobs)) {
  const atlas = new S.TextureAtlas(fs.readFileSync(job.atlas, 'utf8'));
  const loader = new S.AtlasAttachmentLoader(atlas);
  const bytes = fs.readFileSync(job.skeleton);
  const data = /^\s*\{\s*"/.test(bytes.toString('utf8', 0, 80))
    ? new S.SkeletonJson(loader).readSkeletonData(bytes.toString('utf8'))
    : new S.SkeletonBinary(loader).readSkeletonData(new Uint8Array(bytes));
  for (const [name, file] of Object.entries(job.split_animations || {})) {
    const raw = new Uint8Array(fs.readFileSync(file));
    const input = new BinaryInput(raw, job.string_table);
    if (input.readString() !== name) throw Error('Split animation name mismatch: ' + name);
    const animation = new S.SkeletonBinary(loader).readAnimation(input, name, data);
    if (input.index !== raw.length) throw Error('Unconsumed animation bytes: ' + name);
    data.animations = data.animations.filter(a => a.name !== name);
    data.animations.push(animation);
  }
  const skeleton = new S.Skeleton(data);
  if (job.skin) skeleton.setSkinByName(job.skin);
  if (job.skins) {
    const skin = new S.Skin('original-combined');
    for (const name of job.skins) {
      const part = data.findSkin(name);
      if (!part && !job.ignore_missing_skins) throw Error('Missing skin: ' + name);
      if (part) skin.addSkin(part);
    }
    skeleton.setSkin(skin);
  }
  const animation = job.animation ? data.findAnimation(job.animation) : null;
  if (job.animation && !animation) throw Error('Missing animation: ' + job.animation);
  const duration = animation?.duration || 0;
  const fps = job.fps || 12;
  // Include the endpoint so non-looping initial clips can reach their end pose.
  const times = duration > 0 ? Array.from({length: Math.ceil(duration * fps)}, (_, i) => i / fps) : [0];
  if (duration > 0) times.push(duration);
  const frames = [], boneFrames = [], slotFrames = [];
  for (const time of times) {
    skeleton.setToSetupPose();
    if (animation) animation.apply(skeleton, 0, time, false, [], 1, S.MixBlend.replace, S.MixDirection.mixIn);
    skeleton.updateWorldTransform(S.Physics.none);
    const bones = {};
    for (const bone of skeleton.bones) bones[bone.data.name] = {
      x: bone.worldX, y: bone.worldY, a: bone.a, b: bone.b, c: bone.c, d: bone.d,
      scale_x: bone.scaleX, scale_y: bone.scaleY, active: bone.active,
    };
    boneFrames.push(bones);
    slotFrames.push(Object.fromEntries(skeleton.drawOrder.map((slot, order) => [slot.data.name, {
      attachment: slot.getAttachment()?.name || '', bone: slot.bone.data.name, order,
    }])));
    const meshes = [], clipper = new S.SkeletonClipping();
    for (let order = 0; order < skeleton.drawOrder.length; order++) {
      const slot = skeleton.drawOrder[order], attachment = slot.getAttachment();
      if (attachment instanceof S.ClippingAttachment) { clipper.clipStart(slot, attachment); continue; }
      let vertices, triangles;
      if (attachment instanceof S.RegionAttachment) {
        vertices = new Array(8).fill(0);
        attachment.computeWorldVertices(slot, vertices, 0, 2);
        triangles = [0, 1, 2, 2, 3, 0];
      } else if (attachment instanceof S.MeshAttachment) {
        vertices = new Array(attachment.worldVerticesLength).fill(0);
        attachment.computeWorldVertices(slot, 0, attachment.worldVerticesLength, vertices, 0, 2);
        triangles = Array.from(attachment.triangles);
      } else { clipper.clipEndWithSlot(slot); continue; }
      if (!slot.bone.active || slot.color.a * attachment.color.a <= 0) { clipper.clipEndWithSlot(slot); continue; }
      let uv = Array.from(attachment.uvs);
      if (clipper.isClipping()) {
        // Spine 4.2's no-render variant returns x/y/u/v interleaved.
        clipper.clipTrianglesUnpacked(vertices, triangles, triangles.length, uv);
        vertices = Array.from(clipper.clippedVertices);
        uv = Array.from(clipper.clippedUVs);
        triangles = Array.from(clipper.clippedTriangles);
      }
      if (triangles.length) meshes.push({order, slot: slot.data.name, attachment: attachment.name,
        page: attachment.region.page.name, vertices, uv, triangles, blend: slot.data.blendMode,
        color: [slot.color.r * attachment.color.r, slot.color.g * attachment.color.g,
          slot.color.b * attachment.color.b, slot.color.a * attachment.color.a]});
      clipper.clipEndWithSlot(slot);
    }
    clipper.clipEnd();
    frames.push(meshes);
  }
  // Events retain their original times; playback dispatches them separately
  // from mesh samples and therefore does not quantize events to the sample rate.
  const events = [];
  for (const timeline of animation?.timelines || []) if (timeline instanceof S.EventTimeline)
    for (const event of timeline.events) events.push({time: event.time, name: event.data.name,
      int_value: event.intValue, float_value: event.floatValue, string_value: event.stringValue,
      audio: event.data.audioPath, volume: event.volume, balance: event.balance});
  result[id] = {animation: animation?.name || '', duration, fps, sample_times: times,
    frames, bone_frames: boneFrames, slot_frames: slotFrames, events,
    sampling: {continuous_original_skeleton: false, method: 'Original Spine 4.2 sample evaluation, no physics'},
    available_animations: data.animations.map(a => ({name: a.name, duration: a.duration}))};
}
fs.writeFileSync(process.argv[3], JSON.stringify(result));
console.log('Converted travel skeleton datasets:', Object.keys(result).length);
