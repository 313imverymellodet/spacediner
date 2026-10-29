// Builds the App Store product page header video (21:9, 3840 x 1646): the logo on the starfield at the left and two
// portrait gameplay recordings in phone frames at the right, with the game's music (the header starts muted; people
// can unmute it).
// Usage: swift tools/header_video.swift <LilitaOne.ttf> <music> <out.mp4> <seconds> <clip1.mov> <start1> <clip2.mov> <start2>
import AppKit
import AVFoundation
import CoreText

let a = CommandLine.arguments
CTFontManagerRegisterFontsForURL(URL(fileURLWithPath: a[1]) as CFURL, .process, nil)
let music = AVURLAsset(url: URL(fileURLWithPath: a[2]))
let out = URL(fileURLWithPath: a[3])
let length = CMTime(seconds: Double(a[4])!, preferredTimescale: 600)
let clips = [(AVURLAsset(url: URL(fileURLWithPath: a[5])), Double(a[6])!), (AVURLAsset(url: URL(fileURLWithPath: a[7])), Double(a[8])!)]

let W: CGFloat = 3840, H: CGFloat = 1646
let phoneH: CGFloat = 1416, phoneW = (phoneH * 1320 / 2868).rounded()
let gap: CGFloat = 120, rim: CGFloat = 18, radius: CGFloat = 86
// phone screens, in top-left coordinates (what the video transforms use)
let holes = [CGRect(x: 2150, y: (H - phoneH) / 2, width: phoneW, height: phoneH),
             CGRect(x: 2150 + phoneW + gap, y: (H - phoneH) / 2, width: phoneW, height: phoneH)]

func hex(_ s: String, _ alpha: CGFloat = 1) -> NSColor {
    let v = Int(s.dropFirst(), radix: 16)!
    return NSColor(srgbRed: CGFloat(v >> 16 & 255) / 255, green: CGFloat(v >> 8 & 255) / 255, blue: CGFloat(v & 255) / 255, alpha: alpha)
}

// ---- overlay: everything except the gameplay, with transparent holes where the screens go ----
func overlay() -> CGImage {
    let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(W), pixelsHigh: Int(H), bitsPerSample: 8, samplesPerPixel: 4,
                               hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    hex("#1d1440").setFill(); NSRect(x: 0, y: 0, width: W, height: H).fill()
    let glow = NSGradient(colors: [hex("#ff5c8a", 0.30), hex("#ff5c8a", 0)])!
    glow.draw(fromCenter: NSPoint(x: 1050, y: H / 2), radius: 0, toCenter: NSPoint(x: 1050, y: H / 2), radius: 1150, options: [])
    var seed: UInt64 = 5
    func rnd() -> CGFloat { seed = seed &* 6364136223846793005 &+ 1442695040888963407; return CGFloat(seed >> 33) / CGFloat(1 << 31) }
    for i in 0..<220 {
        let r = 3 + rnd() * 8
        (i % 5 == 0 ? hex("#ffc93c", 0.9) : NSColor(white: 1, alpha: 0.35 + rnd() * 0.6)).setFill()
        NSBezierPath(ovalIn: NSRect(x: rnd() * W, y: rnd() * H, width: r, height: r)).fill()
    }
    func word(_ s: String, _ size: CGFloat, _ color: NSColor, _ cy: CGFloat, tilt: CGFloat = 0) {
        let font = NSFont(name: "LilitaOne", size: size) ?? NSFont.boldSystemFont(ofSize: size)
        let sh = NSShadow(); sh.shadowColor = NSColor(white: 0, alpha: 0.55); sh.shadowOffset = NSSize(width: 0, height: -size * 0.04); sh.shadowBlurRadius = 0
        let str = NSAttributedString(string: s, attributes: [.font: font, .foregroundColor: color, .shadow: sh])
        let sz = str.size()
        NSGraphicsContext.saveGraphicsState()
        let t = NSAffineTransform(); t.translateX(by: 1050, yBy: cy); t.rotate(byDegrees: tilt); t.concat()
        str.draw(at: NSPoint(x: -sz.width / 2, y: -sz.height / 2))
        NSGraphicsContext.restoreGraphicsState()
    }
    word("MILKY", 190, hex("#ffc93c"), H / 2 + 400, tilt: 4)
    word("SPACE", 380, .white, H / 2 + 140)
    word("DINER", 380, hex("#ff5c8a"), H / 2 - 210)
    word("COOK · SERVE · LAUNCH TO MARS", 78, NSColor(white: 1, alpha: 0.7), H / 2 - 470)
    // phone bodies: a white rim and a soft shadow around each screen, then punch the screen out
    for hole in holes {
        let r = NSRect(x: hole.minX, y: H - hole.maxY, width: hole.width, height: hole.height)
        NSGraphicsContext.saveGraphicsState()
        let drop = NSShadow(); drop.shadowColor = NSColor(white: 0, alpha: 0.5); drop.shadowOffset = NSSize(width: 0, height: -20); drop.shadowBlurRadius = 50; drop.set()
        NSColor.white.setFill(); NSBezierPath(roundedRect: r.insetBy(dx: -rim, dy: -rim), xRadius: radius + rim, yRadius: radius + rim).fill()
        NSGraphicsContext.restoreGraphicsState()
        NSGraphicsContext.current!.compositingOperation = .clear
        NSBezierPath(roundedRect: r, xRadius: radius, yRadius: radius).fill()
        NSGraphicsContext.current!.compositingOperation = .sourceOver
    }
    NSGraphicsContext.restoreGraphicsState()
    return rep.cgImage!
}

let sem = DispatchSemaphore(value: 0)
Task {
    let comp = AVMutableComposition()
    var layers: [AVMutableVideoCompositionLayerInstruction] = []
    for (i, (asset, start)) in clips.enumerated() {
        let src = try await asset.loadTracks(withMediaType: .video).first!
        let natural = try await src.load(.naturalSize)
        let track = comp.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
        try track.insertTimeRange(CMTimeRange(start: CMTime(seconds: start, preferredTimescale: 600), duration: length), of: src, at: .zero)
        let s = holes[i].height / natural.height
        let li = AVMutableVideoCompositionLayerInstruction(assetTrack: track)
        li.setTransform(CGAffineTransform(scaleX: s, y: s).concatenating(CGAffineTransform(translationX: holes[i].minX, y: holes[i].minY)), at: .zero)
        layers.append(li)
    }
    let mSrc = try await music.loadTracks(withMediaType: .audio).first!
    let mLen = try await music.load(.duration)
    let aTrack = comp.addMutableTrack(withMediaType: .audio, preferredTrackID: kCMPersistentTrackID_Invalid)!
    var at = CMTime.zero
    while at < length { let chunk = CMTimeMinimum(mLen, length - at); try aTrack.insertTimeRange(CMTimeRange(start: .zero, duration: chunk), of: mSrc, at: at); at = at + chunk }

    let instr = AVMutableVideoCompositionInstruction()
    instr.timeRange = CMTimeRange(start: .zero, duration: length)
    instr.layerInstructions = layers
    let vc = AVMutableVideoComposition()
    vc.renderSize = CGSize(width: W, height: H)
    vc.frameDuration = CMTime(value: 1, timescale: 30)
    vc.instructions = [instr]
    let parent = CALayer(), video = CALayer(), over = CALayer()
    for l in [parent, video, over] { l.frame = CGRect(x: 0, y: 0, width: W, height: H) }
    over.contents = overlay()
    parent.addSublayer(video); parent.addSublayer(over)
    vc.animationTool = AVVideoCompositionCoreAnimationTool(postProcessingAsVideoLayer: video, in: parent)

    try? FileManager.default.removeItem(at: out)
    let export = AVAssetExportSession(asset: comp, presetName: AVAssetExportPresetHighestQuality)!
    export.videoComposition = vc
    try await export.export(to: out, as: .mp4)
    print("wrote \(out.path)")
    sem.signal()
}
sem.wait()
