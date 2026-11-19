function poseLift(view, relax, phase) {
    const frame = Math.round(phase * 48);
    const u = Math.min(1, frame / 9);
    const settle = u * u * (3 - 2 * u);
    const t = 2 * Math.PI * Math.max(0, frame - 9) / 39;
    view.pose(relax, 0);
    const sk = view.cur.skeleton;
    const bone = name => sk.findBone("F_" + name);
    function bend(name, degrees) { bone(name).rotation += degrees * settle; }
    // Face-local X points up and Y points left on the original rig.
    for (const eye of ["Eye_1", "Eye_2"]) {
        bone(eye).x -= 1.6 * settle;
        bone(eye).y += 3.3 * settle;
    }
    bend("Eye_3", 9);
    bend("Eye_4", -5);
    bone("Eye_3").x += 2.2 * settle;
    if (frame >= 3) {
        sk.setAttachment("F_Mouth_3", null);
        sk.setAttachment("F_Mouth_2", "F_Mouth_2");
        bend("Mouth", -5);
    }
    // Set an anatomical segment's bearing in world space. The rig has mirrored
    // joints, so measure both vectors in the actual parent's coordinate system.
    function hang(joint, end, dx, dy) {
        sk.updateWorldTransform();
        const b = bone(joint), tip = bone(end);
        const local = (x,y) => b.parent.worldToLocal({x,y});
        const a = local(b.worldX,b.worldY), e = local(tip.worldX,tip.worldY);
        const d = local(b.worldX+dx,b.worldY+dy);
        const delta = Math.atan2(d.y-a.y,d.x-a.x) - Math.atan2(e.y-a.y,e.x-a.x);
        bend(joint, Math.atan2(Math.sin(delta),Math.cos(delta))*180/Math.PI);
    }
    // The torso folds forward over the belt. The neck follows its weight.
    bend("Waist", -99);
    bend("Breast", -22 + .8*Math.sin(t));
    bend("Head", 8 + .9*Math.sin(t-.6));
    hang("Arm_L","Forearm_L",-.25,-1);
    hang("Forearm_L","Hand_L",-.10,-1);
    hang("Arm_R","Forearm_R",.27,-1);
    hang("Forearm_R","Hand_R",.10,-1);
    // These long coat panels hang from the chest, separately from the stiff collar.
    for (const panel of ["Clothes_89","Clothes_93","Clothes_99","Clothes_108"]) {
        bend(panel, 103);
    }
    // Shoe controls are the leg IK targets. Lower both rather than cycling them.
    for (const [suffix, dx, dy] of [["L",-26,4],["R",-10,-8]]) {
        const shoe = bone("Shoes_"+suffix);
        const p = shoe.parent.worldToLocal({x:shoe.worldX+dx*settle,y:shoe.worldY+dy*settle});
        shoe.x = p.x; shoe.y = p.y;
        bend("Shoes_"+suffix,suffix === "L" ? -32 : -24);
    }
    bend("Tail_2", 5 + 1.5*Math.sin(t-.4));
    bend("Clothes_120", -14 + 1.5*Math.sin(t-.7));
    sk.updateWorldTransform();
    const waist = bone("Waist"), z = .44 + .09*settle;
    view.view = {x:108-waist.worldX*z,y:124+waist.worldY*z,z};
}

// Tint the face under the original eyes and hair, on a lift-only texture.
function pickupTexture(image) {
    const c = document.createElement("canvas");
    c.width = image.width;
    c.height = image.height;
    const g = c.getContext("2d");
    g.drawImage(image, 0, 0);
    g.scale(c.width / 692, c.height / 692);
    // F_Face is packed counterclockwise at (72, 2), logical size 109 x 100.
    g.translate(72, 111);
    g.rotate(-Math.PI / 2);
    for (const x of [24, 87]) {
        g.save();
        g.translate(x, 76);
        g.scale(14, 6);
        const pink = g.createRadialGradient(0, 0, 0, 0, 0, 1);
        pink.addColorStop(0, "rgba(229,116,133,.63)");
        pink.addColorStop(1, "rgba(229,116,133,0)");
        g.fillStyle = pink;
        g.fillRect(-1, -1, 2, 2);
        g.restore();
    }
    return c;
}
