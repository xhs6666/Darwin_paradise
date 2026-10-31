"""Regenerate the reference UI's original, tintable line icons and nine-slice surfaces."""
from pathlib import Path
import json
import math
import uuid
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/UI'
S = 4

def meta(path, sprite=True, border=0):
    target = Path(str(path) + '.meta')
    guid = uuid.uuid4().hex
    assert len(guid) == 32 and all(c in '0123456789abcdef' for c in guid)
    if target.exists():
        return
    target.write_text(f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
  isReadable: 0
  textureType: {8 if sprite else 0}
  spriteMode: {1 if sprite else 0}
  spritePixelsToUnits: 100
  spriteMeshType: 1
  alphaIsTransparency: 1
  textureCompression: 0
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  spriteBorder: {{x: {border}, y: {border}, z: {border}, w: {border}}}
''', encoding='utf-8')

def canvas():
    im = Image.new('RGBA', (64*S,64*S))
    return im, ImageDraw.Draw(im)

def line(d, points, width=2.4):
    p = [(round(x*S),round(y*S)) for x,y in points]
    d.line(p, fill='white', width=round(width*S), joint='curve')
    r=width*S/2
    for x,y in (p[0],p[-1]): d.ellipse((x-r,y-r,x+r,y+r), fill='white')

def ellipse(d, box, width=2.4):
    d.ellipse(tuple(round(v*S) for v in box), outline='white', width=round(width*S))

def bez(d, points, width=2.4):
    p=[]
    for i in range(61):
        t=i/60; u=1-t
        p.append((u**3*points[0][0]+3*u*u*t*points[1][0]+3*u*t*t*points[2][0]+t**3*points[3][0],
                  u**3*points[0][1]+3*u*u*t*points[1][1]+3*u*t*t*points[2][1]+t**3*points[3][1]))
    line(d,p,width)

def save_icon(name, draw):
    im,d=canvas();draw(d);p=OUT/'Icons'/f'{name}.png';p.parent.mkdir(parents=True,exist_ok=True)
    im.resize((96,96),Image.Resampling.LANCZOS).save(p);meta(p)

def leaf(d):
    bez(d,[(13,49),(6,21),(29,16),(51,12)]);bez(d,[(51,12),(53,38),(37,58),(13,49)]);line(d,[(10,54),(40,25)])
def dna(d):
    bez(d,[(16,9),(16,32),(48,32),(48,55)]);bez(d,[(48,9),(48,32),(16,32),(16,55)])
    for y,a in [(13,20),(23,27),(41,27),(51,20)]:line(d,[(a,y),(64-a,y)])
def sun(d):
    ellipse(d,(23,23,41,41))
    for i in range(8):
        a=i*math.pi/4;line(d,[(32+15*math.cos(a),32+15*math.sin(a)),(32+23*math.cos(a),32+23*math.sin(a))])
def people(d):
    ellipse(d,(24,11,40,27));bez(d,[(18,51),(18,29),(46,29),(46,51)]);line(d,[(18,51),(46,51)])
    ellipse(d,(9,16,20,28));ellipse(d,(44,16,55,28));bez(d,[(9,47),(5,35),(13,31),(18,33)]);bez(d,[(55,47),(59,35),(51,31),(46,33)])
def pin(d):
    bez(d,[(32,56),(6,30),(16,10),(32,10)]);bez(d,[(32,10),(48,10),(58,30),(32,56)]);ellipse(d,(25,21,39,35))
def run(d):
    ellipse(d,(32,7,42,17));line(d,[(16,27),(26,22),(39,31),(49,25)]);line(d,[(30,25),(25,38),(38,43),(41,55)]);line(d,[(25,38),(18,50),(9,50)])
def egg(d):
    bez(d,[(32,9),(19,9),(5,53),(32,55)]);bez(d,[(32,55),(59,53),(45,9),(32,9)])
def baby(d):
    bez(d,[(38,13),(18,2),(11,24),(18,44)]);bez(d,[(18,44),(22,61),(50,55),(45,40)]);bez(d,[(45,40),(36,29),(40,24),(38,13)]);ellipse(d,(27,16,35,24));line(d,[(27,34),(24,42),(34,44)])
def storage(d):
    ellipse(d,(14,10,50,24))
    for y in (25,35,45):bez(d,[(14,y),(14,y+15),(50,y+15),(50,y)])
    line(d,[(14,17),(14,46)]);line(d,[(50,17),(50,46)])
def mountain(d):line(d,[(8,49),(23,17),(34,36),(41,25),(56,49),(8,49)])
def repair(d):
    ellipse(d,(10,10,54,54));line(d,[(32,21),(32,43)]);line(d,[(21,32),(43,32)])
def bubbles(d):
    for b in [(10,29,27,46),(23,10,40,27),(37,32,54,49)]:ellipse(d,b)
def butterfly(d):
    bez(d,[(31,36),(0,4),(9,51),(31,42)]);bez(d,[(33,36),(64,4),(55,51),(33,42)]);line(d,[(29,51),(35,28)]);ellipse(d,(24,46,33,56));ellipse(d,(34,45,43,55))
def gear(d):
    pts=[]
    for i in range(32):
        a=i*math.tau/32;r=24 if i%4 in (0,3) else 19;pts.append((32+r*math.cos(a),32+r*math.sin(a)))
    line(d,pts+[pts[0]]);ellipse(d,(24,24,40,40))
def planet(d):
    ellipse(d,(14,10,50,54));bez(d,[(9,42),(-3,31),(65,9),(57,25)]);bez(d,[(57,25),(49,39),(6,55),(9,42)])
def refresh(d):
    bez(d,[(48,22),(28,-1),(2,32),(19,47)]);bez(d,[(16,42),(36,65),(62,32),(45,17)]);line(d,[(39,22),(49,22),(49,12)]);line(d,[(25,42),(15,42),(15,52)])
def dice(d):
    line(d,[(32,8),(53,20),(53,44),(32,56),(11,44),(11,20),(32,8),(32,32),(53,20)])
    line(d,[(11,20),(32,32),(32,56)])
    for x,y in [(23,22),(39,22),(20,35),(41,37),(43,45)]:ellipse(d,(x-1,y-1,x+1,y+1),2)
def clock(d):ellipse(d,(10,10,54,54));line(d,[(32,18),(32,33),(42,39)])
def arrow(d):line(d,[(17,32),(50,32)]);line(d,[(29,18),(15,32),(29,46)])
def play(d):d.polygon([(22*S,12*S),(52*S,32*S),(22*S,52*S)],fill='white')
def pause(d):
    for x in (21,37):d.rounded_rectangle((x*S,13*S,(x+7)*S,51*S),radius=2*S,fill='white')
def network(d):
    line(d,[(32,16),(14,47),(50,47),(32,16)])
    for x,y in [(32,13),(12,49),(52,49)]:ellipse(d,(x-7,y-7,x+7,y+7),3)
def folder(d):line(d,[(9,50),(9,16),(28,16),(34,23),(55,23),(55,50),(9,50)]);line(d,[(10,28),(54,28)])
def save(d):line(d,[(12,10),(44,10),(53,19),(53,54),(12,54),(12,10)]);line(d,[(22,10),(22,26),(42,26),(42,10)]);line(d,[(22,53),(22,36),(43,36),(43,53)])
def home(d):line(d,[(8,31),(32,10),(56,31)]);line(d,[(16,26),(16,54),(47,54),(47,26)]);line(d,[(26,54),(26,39),(38,39),(38,54)])
def exit_icon(d):line(d,[(33,11),(11,11),(11,53),(33,53)]);line(d,[(24,32),(54,32)]);line(d,[(43,22),(54,32),(43,42)])
def star(d):
    pts=[]
    for i in range(10):
        a=-math.pi/2+i*math.pi/5;r=24 if i%2==0 else 11;pts.append((32+r*math.cos(a),32+r*math.sin(a)))
    line(d,pts+[pts[0]])
def star_filled(d):
    pts=[]
    for i in range(10):
        a=-math.pi/2+i*math.pi/5;r=24 if i%2==0 else 11;pts.append(((32+r*math.cos(a))*S,(32+r*math.sin(a))*S))
    d.polygon(pts,fill='white')
def thermometer(d):
    bez(d,[(27,39),(11,57),(53,65),(38,39)]);line(d,[(27,39),(27,12)]);bez(d,[(27,12),(27,3),(38,3),(38,12)]);line(d,[(38,12),(38,39)]);line(d,[(32,22),(32,46)],3)
def drop(d):bez(d,[(32,8),(29,18),(7,39),(19,51)]);bez(d,[(19,51),(44,72),(63,41),(32,8)])
def sprout(d):line(d,[(32,55),(32,29)]);bez(d,[(32,34),(4,38),(8,13),(28,24)]);bez(d,[(32,34),(60,38),(56,13),(36,24)])
def layers(d):
    for y in (22,32,42):line(d,[(10,y),(32,y+12),(54,y),(32,y-12),(10,y)])
def ore(d):line(d,[(17,12),(44,12),(57,32),(44,53),(17,53),(6,32),(17,12),(32,32),(44,12)]);line(d,[(32,32),(44,53)]);line(d,[(32,32),(6,32)])

icons = dict(leaf=leaf,dna=dna,sun=sun,people=people,pin=pin,run=run,egg=egg,baby=baby,storage=storage,
             mountain=mountain,repair=repair,bubbles=bubbles,butterfly=butterfly,gear=gear,planet=planet,
             refresh=refresh,dice=dice,clock=clock,back=arrow,play=play,pause=pause,network=network,
             folder=folder,save=save,home=home,exit=exit_icon,star=star,star_filled=star_filled,layers=layers)
icons.update(time_play=play,time_pause=pause,ui_icon_prev=arrow,
             ui_icon_next=lambda d:line(d,[(25,16),(41,32),(25,48)]),
             btn_param_temp=thermometer,btn_param_hum=drop,btn_param_terrain=mountain,btn_param_seed=sprout,
             layer_terrain=mountain,layer_climate=sun,layer_ore=ore,layer_population=network,entity_template_default=bubbles)
for name,draw in icons.items():save_icon(name,draw)

# ★ 新增：圆角表面用九宫格资源，缩放不会改变业务 RectTransform 或按钮命中范围。
surfaces={
 'panel':('#091720','#294a5b'), 'button':('#10212d','#315265'),
 'hover':('#17382f','#4ee5b5'), 'pressed':('#0e2e29','#4ee5b5'),
 'primary':('#4ee5b5','#6df6cc'), 'primary_hover':('#76f2ce','#9affdf'),
 'primary_pressed':('#32c59b','#6df6cc'), 'field':('#10202c','#35566a'),
 'danger':('#10202b','#d87070'), 'track':('#1b303e','#263f4e'),
 'fill':('#4ee5b5','#4ee5b5'), 'thumb':('#effbf8','#92e9d0'),
 'selected':('#143831','#4ee5b5')}
for name,(fill,stroke) in surfaces.items():
    im,d=canvas()
    if name=='thumb':
        d.ellipse((S,S,63*S,63*S),fill=fill,outline=stroke,width=S)
    else:
        if name=='panel':fill=(*tuple(bytes.fromhex(fill[1:])),250)
        d.rounded_rectangle((S,S,63*S,63*S),radius=12*S,fill=fill,outline=stroke,width=S)
    path=OUT/'Surfaces'/f'{name}.png';path.parent.mkdir(parents=True,exist_ok=True)
    im.resize((64,64),Image.Resampling.LANCZOS).save(path);meta(path,True,16)

grid=Image.new('RGBA',(40,40),(0,0,0,0));dot=ImageDraw.Draw(grid)
dot.ellipse((19,19,20,20),fill=(93,156,178,100))
grid_path=OUT/'Surfaces/grid.png';grid.save(grid_path);meta(grid_path,False)
grid_meta=Path(str(grid_path)+'.meta');grid_meta.write_text(grid_meta.read_text().replace('wrapU: 1','wrapU: 0').replace('wrapV: 1','wrapV: 0'),encoding='utf-8')

for name,stroke in [('diamond','#85B0E8'),('diamond_selected','#4EE5B5')]:
    im=Image.new('RGBA',(180*S,80*S));d=ImageDraw.Draw(im)
    d.polygon([(90*S,S),(179*S,40*S),(90*S,79*S),(S,40*S)],fill='#10212D',outline=stroke,width=S)
    path=OUT/'Surfaces'/f'{name}.png';im.resize((360,160),Image.Resampling.LANCZOS).save(path);meta(path,False)

clear_path=OUT/'Surfaces/clear.png';Image.new('RGBA',(4,4)).save(clear_path);meta(clear_path,False)

config={
 'fontNames':['Microsoft YaHei UI','Microsoft YaHei','Noto Sans CJK SC','Arial'],
 'accent':'#4EE5B5','text':'#EDF4F8','muted':'#99AFBF','ink':'#03211E',
 'border':'#294A5B','panel':'#091720','surface':'#10212D','danger':'#EF7979','favorite':'#F3C76C',
 'layerButtons':[{'id':'layer_ore','label':'矿脉'},{'id':'layer_terrain','label':'地形'},{'id':'layer_climate','label':'气候'},{'id':'layer_population','label':'种群'}],
 'summaryStats':[{'id':'health','label':'生命','format':'F0','bar':True},
                 {'id':'nutrient','label':'饱食度','format':'F1','bar':True},
                 {'id':'size','label':'体型','format':'F2','bar':False}],
 'geneIcons':[{ 'id':'gene_'+key,'icon':icon } for key,icon in {
 'growth':'leaf','reproduction':'dna','nutrition':'sun','environment':'mountain','locomotion':'run',
 'storage':'storage','repair':'repair','asexual':'bubbles','sexual':'butterfly','territory':'pin',
 'oviparous':'egg','viviparous':'baby','population':'people','parasitic':'sun'}.items()]}
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'theme.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Generated {len(icons)} icons, {len(surfaces)} surfaces and theme.json')
