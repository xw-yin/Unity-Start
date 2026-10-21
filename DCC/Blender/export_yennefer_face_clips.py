"""Extract actual authored mimic float curves from Yennefer's original animation bank."""
import sys,json,hashlib
from pathlib import Path
sys.path[:0]=['F:/Unity/Tools/WitcherConversion','F:/Unity/Tools/WitcherConversion/site-packages']
from witcher3_tools.CR2W.dc_anims import load_bin_anims_single
root=Path('F:/Unity/ExtractedModels/Characters')
bank=root/'animations/woman/dialog/yennefer_mimic_layers.w2anims'
rig=Path('F:/Unity/ExtractedModels/Yennefer/characters/models/main_npc/yennefer/h_01_wa__yennefer/h_01_wa__yennefer.w3fac')
clips=[]
for name in ['neutral_anim_combined_face','happy_anim_combined_face','neutral_pose_face','happy_pose_face']:
 anim=load_bin_anims_single(str(bank),anim_name=name,rigPath=str(rig)).animations[0].animation
 tracks=[]
 for t in anim.animBuffer.tracks:
  values=[float(x) for x in t.trackFrames]
  if values and max(abs(x) for x in values)>1e-6:tracks.append({'name':str(t.trackName),'values':values})
 clips.append({'name':name,'duration':anim.duration,'fps':anim.framesPerSecond,'frames':anim.animBuffer.numFrames,'tracks':tracks})
 print('CLIP',name,anim.duration,'frames',anim.animBuffer.numFrames,'bones',len(anim.animBuffer.bones),'tracks',len(tracks),[(t['name'],round(min(t['values']),3),round(max(t['values']),3)) for t in tracks],flush=True)
out=Path('F:/Unity/Project/Start/Assets/ThirdParty/WitcherYenneferDLC/Prepared/Face/clips.json')
out.write_text(json.dumps({'source':bank.relative_to(root).as_posix(),'sourceSHA256':hashlib.sha256(bank.read_bytes()).hexdigest(),'clips':clips},indent=2))
