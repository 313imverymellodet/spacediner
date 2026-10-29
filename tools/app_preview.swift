// Turns a simulator screen recording into an App Store app preview: trims it, scales it to the size Apple asks for
// and lays the game's music under it (simulator recordings have no sound; app previews need an audio track).
// Usage: swift tools/app_preview.swift <recording.mov> <music.m4a|wav> <out.mp4> <startSeconds> <durationSeconds> <width> <height>
import AVFoundation

let a = CommandLine.arguments
let video = AVURLAsset(url: URL(fileURLWithPath: a[1]))
let music = AVURLAsset(url: URL(fileURLWithPath: a[2]))
let out = URL(fileURLWithPath: a[3])
let start = CMTime(seconds: Double(a[4])!, preferredTimescale: 600)
let length = CMTime(seconds: Double(a[5])!, preferredTimescale: 600)
let size = CGSize(width: Double(a[6])!, height: Double(a[7])!)

let sem = DispatchSemaphore(value: 0)
Task {
    let comp = AVMutableComposition()
    let src = try await video.loadTracks(withMediaType: .video).first!
    let vTrack = comp.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
    try vTrack.insertTimeRange(CMTimeRange(start: start, duration: length), of: src, at: .zero)

    // music: loop it if the clip is longer than the track, fade the last second
    let mSrc = try await music.loadTracks(withMediaType: .audio).first!
    let mLen = try await music.load(.duration)
    let aTrack = comp.addMutableTrack(withMediaType: .audio, preferredTrackID: kCMPersistentTrackID_Invalid)!
    var at = CMTime.zero
    while at < length {
        let chunk = CMTimeMinimum(mLen, length - at)
        try aTrack.insertTimeRange(CMTimeRange(start: .zero, duration: chunk), of: mSrc, at: at)
        at = at + chunk
    }
    let mix = AVMutableAudioMix()
    let params = AVMutableAudioMixInputParameters(track: aTrack)
    params.setVolumeRamp(fromStartVolume: 1, toEndVolume: 0, timeRange: CMTimeRange(start: length - CMTime(seconds: 1, preferredTimescale: 600), duration: CMTime(seconds: 1, preferredTimescale: 600)))
    mix.inputParameters = [params]

    // scale (aspect fill) to the preview size, 30 fps
    let natural = try await src.load(.naturalSize)
    let scale = max(size.width / natural.width, size.height / natural.height)
    let tx = (size.width - natural.width * scale) / 2, ty = (size.height - natural.height * scale) / 2
    let layer = AVMutableVideoCompositionLayerInstruction(assetTrack: vTrack)
    layer.setTransform(CGAffineTransform(scaleX: scale, y: scale).concatenating(CGAffineTransform(translationX: tx, y: ty)), at: .zero)
    let instr = AVMutableVideoCompositionInstruction()
    instr.timeRange = CMTimeRange(start: .zero, duration: length)
    instr.layerInstructions = [layer]
    let vc = AVMutableVideoComposition()
    vc.renderSize = size
    vc.frameDuration = CMTime(value: 1, timescale: 30)
    vc.instructions = [instr]

    try? FileManager.default.removeItem(at: out)
    let export = AVAssetExportSession(asset: comp, presetName: AVAssetExportPresetHighestQuality)!
    export.videoComposition = vc
    export.audioMix = mix
    try await export.export(to: out, as: .mp4)
    print("wrote \(out.path)")
    sem.signal()
}
sem.wait()
