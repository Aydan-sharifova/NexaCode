namespace Coding.Application.Security;
public static class ImageUploadPolicy
{
    public static bool HasValidSignature(ReadOnlySpan<byte> content,string mediaType)=>mediaType.ToLowerInvariant() switch
    {
        "image/jpeg"=>content.Length>=3&&content[0]==0xff&&content[1]==0xd8&&content[2]==0xff,
        "image/png"=>content.Length>=8&&content[..8].SequenceEqual(new byte[]{0x89,0x50,0x4e,0x47,0x0d,0x0a,0x1a,0x0a}),
        "image/webp"=>content.Length>=12&&content[..4].SequenceEqual("RIFF"u8)&&content.Slice(8,4).SequenceEqual("WEBP"u8),
        _=>false
    };
}
