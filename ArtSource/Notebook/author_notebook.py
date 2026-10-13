"""Offline authoring recipe. Unity consumes the committed JSON, never this recipe at runtime.

Coordinates are local +Y for weapons, +X for enemies. Reference marks deliberately
exclude paper fills, hatching, fasteners, face details and other decorative ink.
"""
import copy
import json
import math
import pathlib
import random
import html

ROOT = pathlib.Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/PaperDefense/Art"
ART.mkdir(parents=True, exist_ok=True)
PALETTE = [(0.12,.19,.28,1), (.34,.40,.48,.78), (.978,.957,.901,1),
           (.85,.22,.27,1), (.22,.48,.74,1), (.25,.59,.43,1), (.58,.66,.74,.18), (1,.995,.96,1)]
COLORS = ["#1f3047", "#57667a", "#f9f4e6", "#d93845", "#387bbd", "#40966e", "#94a8bd", "#fffef5"]
shapes, clips, recoils = [], [], []

def v(p): return {"x":round(p[0],5), "y":round(p[1],5)}
def mark(points=(), primitive=0, channel=0, width=.023, reference=False, name="ink", alpha=1,
         center=(0,0), radius=0, aspect=1, segments=24):
    return {"Name":name,"Primitive":primitive,"Channel":channel,"Width":width,"Alpha":alpha,
            "Reference":reference,"Points":[v(p) for p in points],"Center":v(center),
            "Radius":radius,"Aspect":aspect,"Segments":segments}

def path(points, ref=False, w=.023, ch=0, name="contour", alpha=1):
    # A tiny authored pen drift, baked into source coordinates. Repeated parts never wobble in motion.
    ink=[]
    for index,a in enumerate(points):
        if index==0: ink.append(a); continue
        b=points[index-1];dx,dy=a[0]-b[0],a[1]-b[1];length=math.hypot(dx,dy)
        divisions=min(5,max(1,math.ceil(length/.11)))
        for j in range(1,divisions):
            t=j/divisions
            drift=math.sin((b[0]*17+b[1]*31+index*2.7+j)*3.1)*.0035*math.sin(t*math.pi)
            ink.append((b[0]+dx*t-dy/max(length,.001)*drift,b[1]+dy*t+dx/max(length,.001)*drift))
        ink.append(a)
    return mark(ink, width=w, channel=ch, reference=ref, name=name, alpha=alpha)
def poly(points,ch=2,alpha=1): return mark(points,primitive=1,channel=ch,alpha=alpha,name="paper-fill")
def ring(x,y,r,ref=False,w=.023,ch=0,aspect=1,name="joint"):
    # Persist circle reference as a polyline so template publication uses identical points.
    if ref:
        return path([(x+math.cos(i*math.tau/24)*r,y+math.sin(i*math.tau/24)*r*aspect) for i in range(25)], True,w,ch,name)
    return mark(primitive=3,center=(x,y),radius=r,aspect=aspect,width=w,channel=ch,name=name)
def disc(x,y,r,ch=0,aspect=1,alpha=1):
    return mark(primitive=2,center=(x,y),radius=r,aspect=aspect,channel=ch,alpha=alpha,name="ink-dot")
def outline(points,ref=True,w=.027): return [poly(points),path(points+[points[0]],ref,w)]
def rect(x,y,w,h,ref=True): return outline([(x,y),(x+w,y),(x+w,y+h),(x,y+h)],ref)
def hatch(x,y,w,h,n=6,ch=0):
    return [path([(x+i*w/n,y),(x+i*w/n+min(.055,w/n),y+h)],w=.009,ch=ch,name="hatching",alpha=.55) for i in range(n)]
def bolts(coords): return [ring(x,y,.022,w=.010,name="bolt") for x,y in coords]
def add(id,marks,template=False):
    if not template:
        for m in marks: m["Reference"]=False
    s={"Id":id,"TemplateId":"template."+id if template else "","ReferenceRevision":3,"Marks":marks}
    shapes.append(s); return s

# Four foundations: carefully shared footprint, clearly different silhouette and mechanics.
p=[(-.43,-.27),(.42,-.28),(.47,-.18),(.44,.22),(.30,.34),(-.31,.33),(-.46,.21)]
m=outline(p)+[path([(-.42,-.21),(.41,-.22)],False,.015),ring(0,.04,.16,True),ring(0,.04,.09,w=.012)]
m+=hatch(-.37,-.26,.22,.08,8)+bolts([(-.34,.20),(.34,.20),(-.32,-.18),(.32,-.18)])
add("chassis.standard",m,True)
p=[(-.48,-.31),(.48,-.31),(.48,.22),(.35,.35),(-.35,.35),(-.48,.22)]
m=outline(p)+rect(-.52,-.27,.12,.43)+rect(.40,-.27,.12,.43)
m+=[ring(-.17,.04,.135,True),ring(.17,.04,.135,True),path([(-.36,-.12),(.36,-.12)],False,.017)]
m+=hatch(-.34,-.28,.67,.11,13)+bolts([(-.44,.09),(.44,.09),(-.43,-.20),(.43,-.20)])
add("chassis.heavy",m,True)
p=[(-.42,-.30),(.41,-.30),(.47,-.13),(.29,.33),(-.29,.33),(-.47,-.13)]
m=outline(p)+rect(-.26,-.23,.52,.17)+[ring(0,.04,.18,True),ring(0,.04,.105,w=.014)]
m+=hatch(-.38,-.15,.15,.20,5)+bolts([(-.21,-.15),(.21,-.15),(-.28,.18),(.28,.18)])
m+=[path([(-.11,-.28),(-.11,-.24),(.11,-.24),(.11,-.28)],False,.013)]
add("chassis.precision",m,True)
p=[(-.49,-.31),(.49,-.31),(.52,-.13),(.47,.28),(-.47,.28),(-.52,-.13)]
m=outline(p)+[path([(-.45,-.10),(.45,-.10)],True,.020)]
for x in [-.24,0,.24]: m += [ring(x,.04,.112,True),ring(x,.04,.055,w=.011)]
m+=hatch(-.43,-.27,.86,.10,18)+bolts([(-.43,.20),(.43,.20),(-.40,-.22),(.40,-.22)])
add("chassis.battery",m,True)

# Weapons turn around origin. Decorative marks never change the assessment outline.
m=rect(-.13,-.12,.26,.29)+rect(-.105,.14,.075,.39)+rect(.03,.14,.075,.39)
m += [ring(0,0,.075,w=.015),path([(-.12,.27),(.12,.27)],True,.028),path([(-.12,.42),(.12,.42)],False,.018)]
m += hatch(-.11,-.09,.21,.08,7)+[path([(-.12,.04),(-.20,.02),(-.20,-.08),(-.12,-.09)],True)]
add("weapon.gun",m,True)
p=[(-.105,-.13),(.105,-.13),(.115,.27),(.067,.50),(0,.68),(-.067,.50),(-.115,.27)]
m=outline(p)+[path([(0,.60),(0,.27)],True,.018),ring(0,.34,.026,w=.014),path([(-.09,.22),(.09,.22)],True)]
m+=hatch(-.075,-.10,.15,.18,6)+[path([(-.09,.08),(.09,.08)],False,.012)]
add("weapon.piercer",m,True)
p=[(-.18,-.13),(.18,-.13),(.20,.14),(.16,.43),(-.16,.43),(-.20,.14)]
m=outline(p)+[poly([(-.13,.32),(.13,.32),(.13,.43),(-.13,.43)],0),ring(0,.39,.17,True,aspect=.33),ring(0,.39,.12,w=.014,aspect=.30),path([(-.19,.12),(.19,.12)],True)]
m+=hatch(-.16,-.09,.31,.13,9)+bolts([(-.13,.14),(.13,.14)])
add("weapon.mortar",m,True)
p=[(-.12,-.13),(.12,-.13),(.10,.12),(.29,.41),(.17,.51),(0,.54),(-.17,.51),(-.29,.41),(-.10,.12)]
# Fill fan with convex separate triangles; contour is intentionally expressive and scalloped.
m=[poly([(-.12,-.13),(.12,-.13),(.12,.14),(-.12,.14)]),poly([(0,.04),(.29,.41),(.17,.51),(0,.54),(-.17,.51),(-.29,.41)])]
m+=[path(p+[p[0]],True),path([(0,.02),(0,.52)],True),path([(0,.04),(-.17,.49)],True),path([(0,.04),(.17,.49)],True)]
m+=hatch(-.085,-.11,.17,.10,5)+[disc(0,.02,.035),path([(-.28,.41),(-.18,.42),(-.10,.49),(0,.48),(.10,.49),(.18,.42),(.28,.41)],False,.012)]
add("weapon.fan",m,True)
p=[(-.18,-.12),(.18,-.12),(.18,.42),(.10,.50),(.10,.12),(-.10,.12),(-.10,.50),(-.18,.42)]
m=rect(-.18,-.12,.36,.24)+rect(-.18,.10,.08,.34)+rect(.10,.10,.08,.34)
m+=[path(p+[p[0]],True),path([(-.10,.26),(.06,.36),(-.06,.46)],True,.022),path([(-.21,.43),(-.10,.50)],True),path([(.10,.50),(.21,.43)],True)]
m+=hatch(-.15,-.09,.30,.09,8)+bolts([(-.14,.20),(.14,.20)])
add("weapon.ricochet",m,True)
p=[(-.15,-.14),(.15,-.14),(.18,.10),(.13,.43),(-.13,.43),(-.18,.10)]
m=outline(p)+rect(-.12,.03,.24,.34)+[path([(-.15,.09),(-.21,.19),(-.18,.47),(-.11,.52),(.11,.52),(.18,.47),(.21,.19),(.15,.09)],True,.031)]
m += [path([(-.09,.35),(.09,.35)],False,.014),path([(-.09,.28),(.09,.28)],False,.014),path([(-.10,.45),(.10,.45)],True,.024)]
m+=hatch(-.12,-.11,.24,.10,7)+bolts([(-.12,.10),(.12,.10)])
add("weapon.stapler",m,True)
m=rect(-.24,.12,.48,.27)+rect(-.08,-.11,.16,.25)+[path([(-.25,.16),(-.18,.13),(-.10,.17),(-.03,.13),(.04,.17),(.11,.13),(.18,.17),(.25,.15)],True,.021)]
m += [path([(-.21,.32),(.21,.32)],False,.013),ring(0,-.01,.045,w=.014)]
m+=hatch(-.21,.18,.42,.10,12)+[disc(x,.35,.010,ch=1) for x in [-.16,-.07,.03,.12,.19]]
add("weapon.blotter",m,True)

# Equipment occupies reserved margins of the foundations, leaving joints open.
m=[]
for sign in [-1,1]:
    p=[(sign*.36,-.23),(sign*.52,-.18),(sign*.51,.20),(sign*.40,.28),(sign*.36,.15)]
    m+=outline(p)+hatch(sign*.40,-.15,sign*.08,.13,4)+bolts([(sign*.44,.08),(sign*.44,-.13)])
add("module.armor",m,True)
m=rect(-.30,-.40,.60,.18)+[path([(-.22,-.22),(-.22,-.13),(.22,-.13),(.22,-.22)],True),path([(-.27,-.34),(.27,-.34)],False,.014)]
for x in [-.19,-.095,0,.095,.19]: m+=[path([(x,-.39),(x,-.27)],False,.016)]
add("module.feed",m,True)
m=[path([(.31,-.15),(.31,.35)],True),ring(.31,.39,.115,True),ring(.31,.39,.059,w=.012),path([(.16,.39),(.46,.39)],True,.014),path([(.31,.24),(.31,.54)],True,.014)]+rect(.25,-.23,.12,.10)
add("module.tracking",m,True)
m=[]
for x in [-.37,.37]: m+=rect(x-.025,-.32,.05,.71)
for y in [-.24,.24]: m+=[path([(-.40,y),(.40,y)],True,.017)]
m+=bolts([(-.37,-.24),(.37,-.24),(-.37,.24),(.37,.24)])
add("module.rail",m,True)

# Enemy key poses: authored local limbs + head/mouth motion, then persisted as independent frames.
def enemy(family,tier,state,phase):
    t=phase; marks=[]
    attack=state=="attack"; death=state=="death"; hit=state=="hit"
    stride=math.sin(t*math.tau)*.16 if state=="walk" else math.sin(t*math.tau)*.018
    bob=abs(math.sin(t*math.tau))*.035 if state=="walk" else math.sin(t*math.tau)*.012
    if family=="doodle":
        hx=.03+(t*.12 if attack else 0); hy=.25+bob
        marks += [disc(hx,hy,.145,2),ring(hx,hy,.145,w=.027),path([(hx,hy-.14),(-.035,-.08)],w=.035,name="body")]
        marks += [path([(-.02,.10),(-.17,.01),(-.23,.12 if attack else -.07)],w=.027,name="arm.back")]
        marks += [path([(-.01,.10),(.16,.04),(.28 if attack else .19,.12 if attack else -.04)],w=.030,name="arm.front")]
        marks += [path([(-.035,-.08),(-.14-stride,-.24),(-.23-stride,-.25)],w=.033,name="leg.back"),path([(-.035,-.08),(.08+stride,-.24),(.18+stride,-.25)],w=.033,name="leg.front")]
        marks += [disc(hx+.054,hy+.025,.018),path([(hx+.035,hy-.055),(hx+.09,hy-.04)],w=.013),path([(hx-.10,hy+.14),(hx-.08,hy+.20),(hx-.03,hy+.15),(hx+.015,hy+.21),(hx+.055,hy+.15)],w=.018)]
    elif family=="runner":
        p=[(-.21,-.01),(-.13,.19),(.00,.31),(.15,.30),(.27,.20),(.25,.07),(.04,-.07)]
        marks+=outline(p,False,.029)
        marks += [path([(-.14,.16),(-.22,.26),(-.08,.22),(-.08,.35),(.015,.29),(.06,.40),(.13,.29)],w=.023,name="spikes")]
        marks += [path([(-.05,-.03),(-.18-stride,-.19),(-.31-stride,-.21)],w=.033,name="leg.back"),path([(.07,-.03),(.09+stride,-.23),(.24+stride,-.24)],w=.033,name="leg.front")]
        marks += [path([(.04,.10),(.20,-.02),(.31,.02 if attack else -.07)],w=.027),path([(-.12,.10),(-.29,.03),(-.34,.11)],w=.025),path([(.15,.21),(.24,.18)],w=.021),disc(.215,.15,.018)]
        if state=="walk": marks += [path([(-.31,.17),(-.44,.19)],w=.013,ch=1),path([(-.34,.03),(-.47,.05)],w=.013,ch=1)]
    elif family=="gnawer":
        p=[(-.28,-.10),(-.29,.10),(-.20,.25),(-.04,.32),(.16,.30),(.30,.18),(.31,-.04),(.18,-.18),(-.10,-.19)]
        marks+=outline(p,False,.031)
        marks += [path([(-.17,-.13),(-.25-stride,-.26),(-.33-stride,-.26)],w=.031),path([(.15,-.13),(.21+stride,-.26),(.32+stride,-.26)],w=.031)]
        gap=.07+(.12*math.sin(t*math.pi) if attack else .01)
        mouth=[(-.03,.09),(.29,.14),(.29,-gap),(-.02,-gap-.04)]
        marks += [poly(mouth,0),path(mouth+[mouth[0]],w=.019),path([(-.03,.08),(.02,.01),(.07,.10),(.12,.03),(.17,.11),(.22,.05),(.27,.12)],w=.016,ch=2)]
        marks += [path([(.00,-gap-.02),(.04,-gap+.03),(.09,-gap-.01),(.14,-gap+.035),(.20,-gap+.005),(.25,-gap+.055)],w=.014,ch=2),disc(.12,.23,.025),path([(.02,.28),(.17,.29)],w=.023)]
        marks += [path([(-.25,.15),(-.31,.21),(-.18,.19)],w=.017)]
    else:
        p=[(-.27,-.12),(-.32,.04),(-.27,.23),(-.12,.33),(.07,.32),(.23,.23),(.29,.08),(.24,-.13),(.08,-.22),(-.14,-.22)]
        marks+=outline(p,False,.030)
        r=.075+(.04*math.sin(t*math.pi) if attack else 0)
        marks += [disc(.23,.08,r,2,aspect=.74),ring(.23,.08,r,w=.025,aspect=.74),disc(.255,.08,r*.48,0,aspect=.74),disc(.11,.23,.022),path([(.03,.28),(.17,.29)],w=.021)]
        marks += [path([(-.17,-.16),(-.23-stride*.5,-.26),(-.32-stride*.5,-.26)],w=.027),path([(.05,-.19),(.12+stride*.5,-.27),(.21+stride*.5,-.27)],w=.027)]
        marks += [path([(-.25,.18),(-.35,.26),(-.24,.25)],w=.015),ring(-.12,.15,.028,w=.012,ch=1),ring(-.19,.03,.022,w=.012,ch=1)]
        if attack: marks += [path([(-.05,-.03),(.06,.00),(.12,.07)],w=.019,name="cheek")]
    if tier=="hatched":
        if family=="doodle": marks+=hatch(-.06,.18,.10,.10,5)
        elif family=="runner": marks+=hatch(-.13,.06,.13,.13,6)
        else: marks+=hatch(-.23,-.04,.16,.15,7)
    elif tier=="sketch":
        for m in marks:
            if m["Channel"]==0: m["Channel"]=1; m["Width"]*=.78
        # Deliberate graphite construction lines: lighter, offset strokes, no random runtime jitter.
        marks += [ring(0,.15,.22,w=.009,ch=1,name="construction")]
    if hit:
        for m in marks:
            for p in m["Points"]: p["x"] -= .055*math.sin(t*math.pi)
            m["Center"]["x"] -= .055*math.sin(t*math.pi)
        marks += [path([(-.36,.17),(-.43,.21)],w=.017,ch=3),path([(-.34,.28),(-.37,.35)],w=.017,ch=3)]
    if family=="runner" and attack:
        angle=-.22*math.sin(t*math.pi);c,s=math.cos(angle),math.sin(angle);lunge=.12*math.sin(t*math.pi)
        for m in marks:
            for p in m["Points"]:
                x,y=p["x"],p["y"]+.24;p["x"]=round(x*c-y*s+lunge,5);p["y"]=round(x*s+y*c-.24,5)
            x,y=m["Center"]["x"],m["Center"]["y"]+.24;m["Center"]=v((x*c-y*s+lunge,x*s+y*c-.24))
    if death:
        angle=-t*math.pi*.46; c,s=math.cos(angle),math.sin(angle)
        for m in marks:
            for p in m["Points"]:
                x,y=p["x"],p["y"]+.22; p["x"]=round(x*c-y*s,5);p["y"]=round(x*s+y*c-.22,5)
            x,y=m["Center"]["x"],m["Center"]["y"]+.22
            m["Center"] = v((x*c-y*s,x*s+y*c-.22));m["Alpha"]*=1-t*.70
        marks += [path([(-.32,-.29),(.38,-.29)],w=.015,ch=1,alpha=1-t*.6)]
    return marks

durations={"doodle":(.9,.58,.36,.17,.50),"runner":(.7,.34,.28,.14,.42),"gnawer":(1,.66,.42,.18,.55),"spitter":(1.1,.72,.50,.20,.60)}
for family in ["doodle","runner","gnawer","spitter"]:
    for tier in ["sketch","outlined","hatched"]:
        for state,duration in zip(["idle","walk","attack","hit","death"],durations[family]):
            id=f"enemy.{family}.{tier}.{state}"; frames=[]
            poses=[0,.25,.5,.75] if state in ["idle","walk"] else [0,.32,.68,1]
            for i,phase in enumerate(poses):
                sid=id if i==0 else f"{id}.{i}"
                add(sid,enemy(family,tier,state,phase))
                frames.append({"Time":round(i*duration/4,5),"ShapeId":sid,"Offset":v((0,0)),"Rotation":0,"Scale":1,"Alpha":1})
            if state=="death": frames[-1]["Alpha"]=0
            clips.append({"Id":id,"Duration":duration,"Loop":state in ["idle","walk"],"Frames":frames})

# Castle paper cutout, hand drawn masonry, heart badge and small flag.
m=rect(-.42,-.34,.84,.55,False)+rect(-.55,-.30,.25,.81,False)+rect(.30,-.30,.25,.81,False)
for x in [-.55,.30]:
    m += [path([(x,.50),(x,.64),(x+.08,.64),(x+.08,.56),(x+.17,.56),(x+.17,.64),(x+.25,.64),(x+.25,.50)],w=.027)]
m += [path([(-.30,.20),(-.30,.34),(-.20,.34),(-.20,.25),(-.06,.25),(-.06,.34),(.06,.34),(.06,.25),(.20,.25),(.20,.34),(.30,.34),(.30,.20)],w=.025)]
m += [path([(-.13,-.34),(-.13,-.10),(-.10,-.01),(0,.04),(.10,-.01),(.13,-.10),(.13,-.34)],w=.025),poly([(-.12,-.33),(.12,-.33),(.12,-.08),(-.12,-.08)],0)]
heart=[(0,.09),(-.13,.19),(-.14,.26),(-.08,.30),(0,.25),(.08,.30),(.14,.26),(.13,.19)]
m += [poly(heart[:5],3),poly([heart[0]]+heart[4:],3),path(heart+[heart[0]],w=.016),path([(.03,.34),(.03,.93)],w=.022),poly([(.04,.91),(.36,.85),(.25,.74),(.04,.78)],3),path([(.04,.91),(.36,.85),(.25,.74),(.04,.78)],w=.015)]
for x in [-.43,.42]: m += [path([(x-.045,.30),(x-.045,.40),(x,.44),(x+.045,.40),(x+.045,.30),(x-.045,.30)],w=.018)]
for y in [-.24,-.11,.02]:
    m += [path([(-.53,y),(-.32,y)],w=.010),path([(.32,y),(.53,y)],w=.010)]
m += hatch(-.51,-.28,.17,.08,7)+hatch(.32,-.28,.17,.08,7)
add("object.castle",m)
add("object.castle.damaged",copy.deepcopy(m)+[path([(-.28,.20),(-.19,.10),(-.25,.03),(-.16,-.09)],w=.022),path([(.47,.24),(.38,.14),(.43,.08),(.34,-.06)],w=.022)])

# Every orthogonal mask has its own composited cell geometry in the source asset.
for mask in range(16):
    m=rect(-.30,-.30,.60,.60,False)
    for bit,(x,y,w,h) in enumerate([( -.30,.28,.60,.23),(.28,-.30,.23,.60),(-.30,-.51,.60,.23),(-.51,-.30,.23,.60)]):
        if mask & (1<<bit): m += rect(x,y,w,h,False)
    m += [path([(-.27,-.06),(.27,-.06)],w=.014),path([(-.27,.15),(.27,.15)],w=.014),path([(-.13,-.28),(-.13,-.06)],w=.011),path([(.10,-.06),(.10,.15)],w=.011)]
    m += hatch(-.25,-.28,.49,.08,10)
    add(f"object.wall.{mask}",m)
m=rect(-.43,-.27,.86,.12,False)
for x in [-.30,-.10,.10,.30]:
    p=[(x-.10,-.15),(x,.31),(x+.10,-.15)]
    m += outline(p,False)+[path([(x,-.12),(x,.22)],w=.012)]
add("object.spikes",m)
for name,ch in [("red",3),("blue",4),("green",5)]:
    p=[(-.31,-.31),(.31,-.31),(.36,-.19),(.30,.20),(.19,.28),(-.19,.28),(-.30,.20),(-.36,-.19)]
    m=outline(p,False)+[poly([(-.27,-.25),(.27,-.25),(.28,.02),(-.28,.02)],ch,alpha=.60),ring(0,.25,.22,w=.029,aspect=.37),disc(0,.25,.17,ch,aspect=.32),path([(-.28,-.07),(-.30,.12),(-.23,.19)],w=.012,ch=7)]
    # Three different shapes as well as RGB ink.
    if name=="red": badge=[(-.08,-.12),(0,.04),(.08,-.12),(0,-.18)]
    elif name=="blue": badge=[(0,.04),(-.09,-.09),(0,-.18),(.09,-.09)]
    else: badge=[(-.09,-.16),(-.09,.01),(.09,.01),(.09,-.16)]
    m += [path(badge+[badge[0]],w=.018,ch=2)]+hatch(-.27,-.29,.50,.06,9)
    add("object.well."+name,m)
    add("object.well."+name+".active",copy.deepcopy(m)+[ring(0,.02,.50,w=.016,ch=ch,aspect=.75)]+[disc(x,y,.024,ch) for x,y in [(-.40,.36),(.38,.40),(.46,-.16)]])

def star(r=.35,ch=0,arms=7):
    return [path([(math.cos(i*math.tau/arms)*r*.30,math.sin(i*math.tau/arms)*r*.30),(math.cos(i*math.tau/arms)*r,math.sin(i*math.tau/arms)*r)],w=.025,ch=ch) for i in range(arms)]
def blot(r=.35,ch=0):
    points=[(math.cos(i*math.tau/24)*r*(1+.18*math.sin(i*2.7)),math.sin(i*math.tau/24)*r*(.75+.12*math.cos(i*1.8))) for i in range(24)]
    # Fan triangles from the center cover this star-shaped ink silhouette without concave fan artifacts.
    m=[poly([(0,0),points[i],points[(i+1)%len(points)]],ch) for i in range(len(points))]
    m += [disc(x,y,s,ch) for x,y,s in [(-r*1.2,.12,.035),(r*1.25,-.08,.028),(.13,r,.025)]]
    return m
add("object.blot",blot())
fx={
 "muzzle":star(.24),"tracer":[path([(0,-.42),(0,.42)],w=.022),path([(-.04,-.34),(-.04,.19)],w=.010,ch=1)],
 "piercer":[path([(0,-.50),(0,.50)],w=.033),path([(-.045,-.40),(-.045,.40)],w=.010,ch=4)],
 "mortar.projectile":[disc(0,0,.075),ring(0,0,.10,w=.012),path([(0,-.12),(-.04,-.22),(.01,-.33)],w=.017,ch=1)],
 "mortar.explosion":star(.48,0,11)+[ring(0,0,.30,w=.024),ring(0,0,.40,w=.010,ch=1)],
 "fan":[path([(0,0),(-.36,.42)],w=.023),path([(0,0),(0,.53)],w=.023),path([(0,0),(.36,.42)],w=.023)],
 "ricochet":[path([(-.43,-.27),(.20,-.05),(-.12,.20),(.43,.38)],w=.025)]+star(.14,4,5),
 "stapler":[path([(-.09,-.07),(-.09,.12),(.09,.12),(.09,-.07)],w=.025)],
 "cleanup":[ring(0,0,.31,w=.020,ch=4),path([(-.30,-.31),(.30,.31)],w=.035,ch=4)]+star(.40,4,6),
 "status.red":[path([(-.13,-.19),(-.19,.02),(-.08,-.02),(0,.25),(.08,.02),(.16,.11),(.14,-.14),(0,-.22),(-.13,-.19)],w=.027,ch=3)],
 "status.blue":star(.25,4,6)+[ring(0,0,.18,w=.015,ch=4)],
 "status.green":[path([(-.19,.20),(0,.27),(.19,.20),(.17,-.01),(.06,-.12)],w=.025,ch=5),path([(-.14,-.03),(-.08,-.17),(.02,-.21)],w=.025,ch=5),path([(.06,.13),(-.02,.03),(.05,-.01),(-.03,-.13)],w=.019,ch=5)],
 "blot.birth":blot(.30)+star(.46,1,7),"blot.absorb":blot(.32)+[ring(0,0,.44,w=.027),ring(0,0,.51,w=.010)],
 "blot.cleanse":star(.38,4,8)+[ring(0,0,.22,w=.022,ch=4)],
 "blot.expire":[ring(0,0,.25,w=.011,ch=1)]+[disc(x,y,.018,1,alpha=.55) for x,y in [(-.30,.20),(.34,.14),(.16,-.28),(-.14,-.26)]],
 "spitter.projectile":blot(.10)+[path([(-.30,0),(-.18,.01)],w=.019,ch=1)],
 "spitter.impact":blot(.25)+star(.39,0,9),"well.activate":star(.43,4,8)+[ring(0,0,.34,w=.018,ch=4)],
 "castle.hit":star(.46,3,9),"well.hit":star(.39,3,7)+[ring(0,0,.19,w=.019)],"structure.destroy":star(.43,1,7)+[path([(-.29,.18),(-.39,.35),(-.22,.30)],w=.022),path([(.20,-.15),(.35,-.22),(.29,-.34)],w=.022)]}
for id,m in fx.items():
    shape=add("fx."+id,m)
    duration=.48 if id in ["mortar.explosion","structure.destroy","blot.absorb"] else .30
    frames=[]
    for i,(scale,alpha) in enumerate([(.55,1),(1,1),(1.30,.50),(1.5,0)]):
        frames.append({"Time":i*duration/4,"ShapeId":shape["Id"],"Offset":v((0,0)),"Rotation":0,"Scale":scale,"Alpha":alpha})
    clips.append({"Id":shape["Id"],"Duration":duration,"Loop":False,"Frames":frames})

for id,y,distance,duration,effect in [("gun",.55,.075,.16,"muzzle"),("piercer",.68,.12,.24,"piercer"),("mortar",.45,.15,.32,"muzzle"),("fan",.55,.085,.22,"fan"),("ricochet",.50,.07,.20,"ricochet"),("stapler",.53,.10,.24,"stapler"),("blotter",.39,.045,.30,"cleanup")]:
    recoils.append({"WeaponId":"weapon."+id,"EffectId":"fx."+effect,"Duration":duration,"Distance":distance,"Muzzle":v((0,y)),"Keys":[{"Time":0,"Value":0},{"Time":duration*.15,"Value":1},{"Time":duration*.40,"Value":.74},{"Time":duration,"Value":0}]})

# UI shapes use exactly the same original marks and normalized coordinates.
for shape in list(shapes):
    if shape["TemplateId"]: add("ui."+shape["Id"],copy.deepcopy(shape["Marks"]))
add("ui.material.pencil",[path([(-.30,-.30),(.24,.35),(.33,.26),(-.23,-.37),(-.35,-.40),(-.30,-.30)],w=.035),path([(-.25,-.30),(.27,.29)],w=.015)])
add("ui.material.pen",[path([(-.22,-.35),(.18,.32),(.29,.26),(-.11,-.40),(-.22,-.35)],w=.034),path([(.16,.25),(.07,.40),(.12,.43),(.29,.34)],w=.020)])
add("ui.material.marker",rect(-.12,-.33,.24,.56,False)+rect(-.14,.23,.28,.13,False)+[path([(-.06,-.33),(-.06,-.43),(.06,-.43),(.06,-.33)],w=.026)])
for name,ch in [("red",3),("blue",4),("green",5)]: add("ui.ink."+name,[disc(0,0,.27,ch),ring(0,0,.29,w=.024)]+star(.15,2,5))
add("ui.tool.wall",copy.deepcopy(next(s for s in shapes if s["Id"]=="object.wall.0")["Marks"]))
add("ui.tool.spikes",copy.deepcopy(next(s for s in shapes if s["Id"]=="object.spikes")["Marks"]))
add("ui.tool.erase",[poly([(-.33,-.20),(-.10,-.36),(.34,.17),(.10,.35)],2),path([(-.33,-.20),(-.10,-.36),(.34,.17),(.10,.35),(-.33,-.20)],w=.027),path([(-.12,-.10),(.12,.10)],w=.020)])
add("ui.tool.select",[path([(-.20,.34),(-.20,-.30),(-.02,-.15),(.11,-.35),(.23,-.27),(.09,-.06),(.31,-.04),(-.20,.34)],w=.027)])

source={"SchemaVersion":1,"Revision":"notebook-ink-2026.10.13-r3","MaximumTerminalTailSeconds":1.2,"MaximumEffects":256,"MaximumDeaths":128,
        "Palette":[dict(zip(["r","g","b","a"],c)) for c in PALETTE],"Shapes":shapes,"Clips":clips,"Recoils":recoils}
with (ROOT/"ArtSource/Notebook/notebook-theme.json").open("w",encoding="utf-8") as f: json.dump(source,f,ensure_ascii=False,indent=2)

def svg_mark(m):
    col=COLORS[m["Channel"]];opacity=m["Alpha"]*PALETTE[m["Channel"]][3]
    if m["Primitive"] in [0,1]:
        points=" ".join(f'{p["x"]},{-p["y"]}' for p in m["Points"])
        if m["Primitive"]==1: return f'<polygon points="{points}" fill="{col}" opacity="{opacity}"/>'
        return f'<polyline points="{points}" fill="none" stroke="{col}" stroke-width="{m["Width"]}" stroke-linecap="round" stroke-linejoin="round" opacity="{opacity}"/>'
    c=m["Center"]; common=f'cx="{c["x"]}" cy="{-c["y"]}" rx="{m["Radius"]}" ry="{m["Radius"]*m["Aspect"]}" opacity="{opacity}"'
    return f'<ellipse {common} fill="{col}"/>' if m["Primitive"]==2 else f'<ellipse {common} fill="none" stroke="{col}" stroke-width="{m["Width"]}"/>'

def sheet(name,ids,cols=5):
    lookup={s["Id"]:s for s in shapes};width=cols*230;height=math.ceil(len(ids)/cols)*210+80
    out=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
         '<defs><pattern id="grid" width="20" height="20" patternUnits="userSpaceOnUse"><path d="M20 0H0V20" fill="none" stroke="#b7cddd" stroke-width=".55" opacity=".60"/></pattern></defs>',
         f'<rect width="100%" height="100%" fill="#f9f4e6"/><rect width="100%" height="100%" fill="url(#grid)"/><path d="M30 0V{height}" stroke="#d77b80" stroke-width="1"/>',
         f'<text x="52" y="38" fill="#1f3047" font-family="monospace" font-size="19">{html.escape(name)}</text>']
    for i,id in enumerate(ids):
        x=(i%cols)*230+115;y=(i//cols)*210+165
        out += [f'<g transform="translate({x},{y}) scale(130)">']+[svg_mark(m) for m in lookup[id]["Marks"]]+['</g>',f'<text x="{x}" y="{y+85}" text-anchor="middle" fill="#1f3047" font-family="monospace" font-size="11">{html.escape(id)}</text>']
    out.append('</svg>');(ART/(name+".svg")).write_text("\n".join(out),encoding="utf-8")
sheet("01-modular-parts",[s["Id"] for s in shapes if s["TemplateId"]])
sheet("02-enemy-families",[f"enemy.{family}.{tier}.idle" for family in ["doodle","runner","gnawer","spitter"] for tier in ["sketch","outlined","hatched"]],3)
sheet("03-enemy-key-poses",[f"enemy.{family}.hatched.{state}"+(".2" if state in ["attack","death"] else "") for family in ["doodle","runner","gnawer","spitter"] for state in ["idle","walk","attack","hit","death"]],5)
sheet("04-notebook-objects",["object.castle","object.castle.damaged","object.spikes","object.blot","object.well.red","object.well.blue","object.well.green"]+[f"object.wall.{i}" for i in range(16)],5)
sheet("05-ink-effects",["fx."+id for id in fx],5)
sheet("06-workshop-icons",[s["Id"] for s in shapes if s["Id"].startswith("ui.")],5)
print(f"Authored {len(shapes)} shapes, {len(clips)} clips, {len(recoils)} recoil curves; canonical JSON and six SVG contact sheets.")
