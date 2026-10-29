// Frames raw simulator screenshots as App Store images: a caption on the title screen's deep purple, the gameplay
// below with rounded corners. Output is exactly the size App Store Connect asks for.
// Usage: swift tools/store_shots.swift <LilitaOne.ttf> <width> <height> <out.png> <screenshot.png> "<caption>" ["<highlight word>"]
import AppKit
import CoreText

let a = CommandLine.arguments
CTFontManagerRegisterFontsForURL(URL(fileURLWithPath: a[1]) as CFURL, .process, nil)
let W = CGFloat(Int(a[2])!), H = CGFloat(Int(a[3])!)
let shot = NSImage(contentsOfFile: a[5])!
let caption = a[6], highlight = a.count > 7 ? a[7] : ""

func hex(_ s: String, _ alpha: CGFloat = 1) -> NSColor {
    let v = Int(s.dropFirst(), radix: 16)!
    return NSColor(srgbRed: CGFloat(v >> 16 & 255) / 255, green: CGFloat(v >> 8 & 255) / 255, blue: CGFloat(v & 255) / 255, alpha: alpha)
}

let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(W), pixelsHigh: Int(H), bitsPerSample: 8, samplesPerPixel: 4,
                           hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
NSGraphicsContext.current!.imageInterpolation = .high

// background: deep purple with a soft pink glow and a few stars
hex("#1d1440").setFill(); NSRect(x: 0, y: 0, width: W, height: H).fill()
let glow = NSGradient(colors: [hex("#ff5c8a", 0.30), hex("#ff5c8a", 0)])!
glow.draw(fromCenter: NSPoint(x: W / 2, y: H * 0.86), radius: 0, toCenter: NSPoint(x: W / 2, y: H * 0.86), radius: W * 0.75, options: [])
var seed: UInt64 = 11
func rnd() -> CGFloat { seed = seed &* 6364136223846793005 &+ 1442695040888963407; return CGFloat(seed >> 33) / CGFloat(1 << 31) }
for i in 0..<70 {
    let r = (2 + rnd() * 5) * W / 1320
    (i % 4 == 0 ? hex("#ffc93c", 0.9) : NSColor(white: 1, alpha: 0.4 + rnd() * 0.5)).setFill()
    NSBezierPath(ovalIn: NSRect(x: rnd() * W, y: H * 0.72 + rnd() * H * 0.28, width: r, height: r)).fill()
}

// caption: two lines max, white with the highlight word in gold
let band = H * 0.21
let font = NSFont(name: "LilitaOne", size: W * 0.085) ?? NSFont.boldSystemFont(ofSize: W * 0.085)
let shadow = NSShadow(); shadow.shadowColor = NSColor(white: 0, alpha: 0.5); shadow.shadowOffset = NSSize(width: 0, height: -W * 0.006); shadow.shadowBlurRadius = 0
let para = NSMutableParagraphStyle(); para.alignment = .center; para.lineSpacing = -W * 0.01
let text = NSMutableAttributedString(string: caption.uppercased(), attributes: [.font: font, .foregroundColor: NSColor.white, .shadow: shadow, .paragraphStyle: para])
if !highlight.isEmpty, let r = caption.uppercased().range(of: highlight.uppercased()) {
    text.addAttribute(.foregroundColor, value: hex("#ffc93c"), range: NSRange(r, in: caption.uppercased()))
}
let box = NSRect(x: W * 0.06, y: H - band, width: W * 0.88, height: band * 0.86)
let fit = text.boundingRect(with: box.size, options: [.usesLineFragmentOrigin])
text.draw(with: NSRect(x: box.minX, y: box.minY + (box.height - fit.height) / 2, width: box.width, height: fit.height), options: [.usesLineFragmentOrigin])

// the gameplay, scaled to fit under the caption, rounded, with a white rim like the in-game cards
let avail = NSRect(x: W * 0.07, y: H * 0.025, width: W * 0.86, height: H - band - H * 0.04)
let s = min(avail.width / shot.size.width, avail.height / shot.size.height)
let sw = shot.size.width * s, sh = shot.size.height * s
let frame = NSRect(x: (W - sw) / 2, y: avail.minY + (avail.height - sh), width: sw, height: sh)
let radius = W * 0.06
NSGraphicsContext.saveGraphicsState()
let drop = NSShadow(); drop.shadowColor = NSColor(white: 0, alpha: 0.45); drop.shadowOffset = NSSize(width: 0, height: -W * 0.012); drop.shadowBlurRadius = W * 0.03; drop.set()
NSColor.white.setFill(); NSBezierPath(roundedRect: frame.insetBy(dx: -W * 0.012, dy: -W * 0.012), xRadius: radius * 1.15, yRadius: radius * 1.15).fill()
NSGraphicsContext.restoreGraphicsState()
NSGraphicsContext.saveGraphicsState()
NSBezierPath(roundedRect: frame, xRadius: radius, yRadius: radius).addClip()
shot.draw(in: frame)
NSGraphicsContext.restoreGraphicsState()

NSGraphicsContext.restoreGraphicsState()
// App Store Connect rejects screenshots with an alpha channel: redraw into an RGB context with no alpha
let cs = CGColorSpace(name: CGColorSpace.sRGB)!
let ctx = CGContext(data: nil, width: Int(W), height: Int(H), bitsPerComponent: 8, bytesPerRow: 0, space: cs,
                    bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
ctx.draw(rep.cgImage!, in: CGRect(x: 0, y: 0, width: W, height: H))
let dest = CGImageDestinationCreateWithURL(URL(fileURLWithPath: a[4]) as CFURL, "public.png" as CFString, 1, nil)!
CGImageDestinationAddImage(dest, ctx.makeImage()!, nil)
CGImageDestinationFinalize(dest)
