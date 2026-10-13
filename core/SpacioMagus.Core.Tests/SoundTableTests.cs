using SpacioMagus.Core.Loading;

namespace SpacioMagus.Core.Tests;

public class SoundTableTests
{
    [Fact]
    public void ParsesFilesAndVolume()
    {
        var sounds = SoundTable.Parse("s.txt", "; commentaire\n[sons]\nui.tap = interface/a.ogg, -2.5\nmg.step = jeu/p1.wav jeu/p2.wav\n");

        Assert.Equal(new[] { "interface/a.ogg" }, sounds["ui.tap"].Files);
        Assert.Equal(-2.5f, sounds["ui.tap"].VolumeDb);
        Assert.Equal(new[] { "jeu/p1.wav", "jeu/p2.wav" }, sounds["mg.step"].Files);
        Assert.Equal(0f, sounds["mg.step"].VolumeDb);
    }

    [Fact]
    public void LeavesUnlistedSoundsOut()
    {
        var sounds = SoundTable.Parse("s.txt", "ui.tap = a.ogg");

        Assert.False(sounds.ContainsKey("mo.spot"));
    }

    [Theory]
    [InlineData("ui.inconnu = a.ogg")]
    [InlineData("ui.tap = a.ogg, fort")]
    [InlineData("ui.tap = a.ogg, 99")]
    [InlineData("ui.tap = , -2")]
    [InlineData("ui.tap = a.ogg, -2, 3")]
    [InlineData("ui.tap = a.ogg\nui.tap = b.ogg")]
    public void RejectsInvalidLines(string content)
    {
        var exception = Assert.Throws<LevelLoadException>(() => SoundTable.Parse("s.txt", content));

        Assert.NotEmpty(exception.Issues);
    }
}
