"""Notebook scenery and tool symbols. Called by the canonical authoring recipe."""


def author_battlefield(add, path, poly, ring, disc, outline, rect, hatch, star, copy, shapes):
    mountain = outline([(-.56,-.34),(-.05,.59),(.58,-.34)],False,.025)
    mountain += [poly([(-.05,.59),(.58,-.34),(.07,-.34)],1,alpha=.16),path([(-.23,.23),(-.12,.16),(-.05,.27),(.06,.15),(.18,.21)],w=.018),path([(-.05,.59),(.07,-.34)],w=.013)]
    mountain += hatch(.12,-.28,.29,.20,8)
    add("terrain.mountain",mountain)
    ridge = []
    # Overlapping convex triangles keep the fill valid for the mesh importer.
    ridge += outline([(-.58,-.32),(-.28,.28),(.06,-.32)],False,.023)
    ridge += outline([(-.20,-.32),(.16,.51),(.58,-.32)],False,.024)
    ridge += [path([(.02,.22),(.10,.17),(.16,.26),(.25,.18)],w=.015)]+hatch(.22,-.27,.23,.17,6)
    add("terrain.ridge",ridge)
    trunk = rect(-.055,-.43,.11,.35,False)
    pine = list(trunk)
    for x,y,w,h in [(0,-.16,.41,.44),(0,.05,.32,.39),(0,.24,.22,.35)]:
        pine += outline([(x-w,y),(x,y+h),(x+w,y)],False,.022)
        pine += [path([(x+.04,y+.04),(x+w*.58,y+.08)],w=.014,ch=5)]
    pine += [path([(-.30,-.44),(.30,-.44)],w=.013,ch=1)]
    add("terrain.pine",pine)
    oak = list(trunk)
    for x,y,r in [(-.22,.10,.23),(.19,.10,.26),(0,.32,.28),(-.03,.02,.26)]:
        oak += [disc(x,y,r,2),ring(x,y,r,w=.024)]
    oak += [path([(-.09,-.21),(-.03,.03),(.10,.14)],w=.024),path([(-.03,.03),(-.17,.13)],w=.018),path([(-.21,.27),(-.16,.34),(-.09,.35)],w=.018,ch=5),path([(.17,-.02),(.24,.03),(.26,.10)],w=.018,ch=5)]
    add("terrain.oak",oak)
    rock=outline([(-.44,-.23),(-.34,.09),(-.10,.28),(.25,.22),(.45,-.13),(.28,-.27)],False,.024)
    rock += [path([(-.10,.28),(.03,-.04),(.45,-.13)],w=.014),path([(.03,-.04),(-.18,-.25)],w=.014)]+hatch(.12,-.22,.15,.12,5)
    add("terrain.rock",rock)
    shrub=[]
    for x,y,r in [(-.22,-.06,.17),(-.02,.05,.22),(.21,-.05,.17)]:
        shrub += [disc(x,y,r,2),ring(x,y,r,w=.022,ch=5)]
    shrub += [path([(-.40,-.22),(.41,-.22)],w=.015,ch=1),path([(-.13,-.19),(-.03,-.03),(.11,-.18)],w=.018)]
    add("terrain.shrub",shrub)
    tar=[disc(0,-.10,.44,0,aspect=.55),ring(0,-.10,.48,w=.022,aspect=.58),path([(-.29,-.09),(-.13,-.02),(.05,-.07)],w=.019,ch=2),ring(.22,-.09,.054,w=.016,ch=2),path([(-.25,.10),(-.25,.27),(-.14,.36)],w=.022),path([(.06,.12),(.10,.31),(.01,.40)],w=.022)]
    add("object.tar",tar)
    repair=rect(-.34,-.32,.68,.40,False)+rect(-.22,.08,.44,.21,False)
    repair += [path([(-.24,.29),(0,.47),(.24,.29)],w=.030),path([(-.14,-.10),(.14,-.10)],w=.069,ch=5),path([(0,-.24),(0,.04)],w=.069,ch=5)]+hatch(-.31,-.29,.15,.09,5)
    add("object.repair",repair)
    entrance=[path([(-.44,-.44),(-.44,.43),(.44,.43),(.44,-.44)],w=.035),path([(-.20,.12),(0,-.11),(.20,.12)],w=.052,ch=3),path([(0,.29),(0,-.11)],w=.052,ch=3)]
    add("object.entrance",entrance)
    site=[path([(-.42,-.19),(.42,.19)],w=.035,ch=4),path([(-.42,.19),(.42,-.19)],w=.035,ch=4)]
    for sign in [-1,1]: site += [path([(sign*.48,-.38),(sign*.48,.38)],w=.026,ch=4)]
    add("object.barricade-site",site)
    for name,source in [("tar","object.tar"),("repair","object.repair")]:
        add("ui.tool."+name,copy.deepcopy(next(s for s in shapes if s["Id"]==source)["Marks"]))
    add("ui.tool.upgrade",[path([(-.24,-.28),(-.24,.06),(-.42,.06),(0,.43),(.42,.06),(.24,.06),(.24,-.28),(-.24,-.28)],w=.033),path([(-.24,-.40),(.24,-.40)],w=.028,ch=4)])
    add("ui.tool.blotter",copy.deepcopy(next(s for s in shapes if s["Id"]=="weapon.blotter")["Marks"]))
    add("ui.tool.paint",[path([(-.22,-.34),(.10,.17),(.23,.10),(-.09,-.41),(-.22,-.34)],w=.029),poly([(.10,.17),(.20,.42),(.39,.32),(.23,.10)],4),path([(.10,.17),(.20,.42),(.39,.32),(.23,.10)],w=.025)])
    add("ui.action.wave",[poly([(-.23,-.33),(.34,0),(-.23,.33)],4),path([(.43,-.34),(.43,.34)],w=.033)])
    add("ui.action.workshop",rect(-.37,-.31,.74,.54,False)+[path([(-.20,.23),(-.20,.38),(.20,.38),(.20,.23)],w=.026),path([(-.37,-.02),(.37,-.02)],w=.020),rect(-.07,-.08,.14,.13,False)[0]])
