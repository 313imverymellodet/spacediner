// Draws the iOS launch (splash) images: the MILKY SPACE DINER logo on a starfield, matching the title screen.
// Usage: swift tools/launch_screen.swift <LilitaOne.ttf> <out-iphone.png> <out-ipad.png>
// The images fill the screen (aspect fill), so everything important stays in the middle.
import AppKit
import CoreText

let args = CommandLine.arguments
CTFontManagerRegisterFontsForURL(URL(fileURLWithPath: args[1]) as CFURL, .process, nil)

func hex(_ s: String, _ a: CGFloat = 1) -> NSColor {
    let v = Int(s.dropFirst(), radix: 16)!
    return NSColor(srgbRed: CGFloat(v >> 16 & 255) / 255, green: CGFloat(v >> 8 & 255) / 255, blue: CGFloat(v & 255) / 255, alpha: a)
}

func draw(width: Int, height: Int, to path: String) {
    let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: width, pixelsHigh: height, bitsPerSample: 8, samplesPerPixel: 4,
                               hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    let w = CGFloat(width), h = CGFloat(height)

    hex("#1d1440").setFill()
    NSRect(x: 0, y: 0, width: w, height: h).fill()
    // soft pink glow behind the logo
    let glow = NSGradient(colors: [hex("#ff5c8a", 0.28), hex("#ff5c8a", 0)])!
    let gc = NSPoint(x: w / 2, y: h * 0.53)
    glow.draw(fromCenter: gc, radius: 0, toCenter: gc, radius: w * 0.62, options: [])

    // stars (fixed seed, so the image is the same every time)
    var seed: UInt64 = 7
    func rnd() -> CGFloat { seed = seed &* 6364136223846793005 &+ 1442695040888963407; return CGFloat(seed >> 33) / CGFloat(1 << 31) }
    for i in 0..<140 {
        let r = 2 + rnd() * 5 * w / 1290
        (i % 5 == 0 ? hex("#ffc93c", 0.9) : NSColor(white: 1, alpha: 0.35 + rnd() * 0.6)).setFill()
        NSBezierPath(ovalIn: NSRect(x: rnd() * w, y: rnd() * h, width: r, height: r)).fill()
    }

    // logo: MILKY (gold, tilted) / SPACE (white) / DINER (pink), with a dark drop outline like the in-game Outline
    let unit = min(w, h * 0.46) / 1290
    func word(_ s: String, size: CGFloat, color: NSColor, centerY: CGFloat, tilt: CGFloat = 0) {
        let font = NSFont(name: "LilitaOne", size: size * unit) ?? NSFont.boldSystemFont(ofSize: size * unit)
        let shadow = NSShadow(); shadow.shadowColor = NSColor(white: 0, alpha: 0.55); shadow.shadowOffset = NSSize(width: 0, height: -10 * unit); shadow.shadowBlurRadius = 0
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color, .shadow: shadow]
        let str = NSAttributedString(string: s, attributes: attrs)
        let sz = str.size()
        NSGraphicsContext.saveGraphicsState()
        let t = NSAffineTransform(); t.translateX(by: w / 2, yBy: centerY); t.rotate(byDegrees: tilt); t.concat()
        str.draw(at: NSPoint(x: -sz.width / 2, y: -sz.height / 2))
        NSGraphicsContext.restoreGraphicsState()
    }
    let cy = h * 0.53
    word("MILKY", size: 150, color: hex("#ffc93c"), centerY: cy + 330 * unit, tilt: 4)
    word("SPACE", size: 300, color: .white, centerY: cy + 110 * unit)
    word("DINER", size: 300, color: hex("#ff5c8a"), centerY: cy - 170 * unit)
    word("COOK · SERVE · EXPAND", size: 62, color: NSColor(white: 1, alpha: 0.62), centerY: cy - 380 * unit)

    NSGraphicsContext.restoreGraphicsState()
    try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: path))
}

draw(width: 1290, height: 2796, to: args[2])   // iPhone (6.7"/6.9" pixel size; scales down on smaller phones)
draw(width: 2048, height: 2732, to: args[3])   // iPad 12.9"/13"
