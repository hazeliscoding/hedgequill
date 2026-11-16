using Hedgequill.Core.Projects;
using Hedgequill.Core.Text;

namespace Hedgequill.TypingProbe;

/// <summary>An invented chapter of at least 10,000 words, the same every run: prose, dialogue, emphasis, a heading, quotes, scene breaks and a few typos.</summary>
internal static class Chapter
{
    private const int Length = 10_000;

    private static readonly string[] Sentences =
    [
        "The fog came in before the boats did.",
        "Mara climbed the tower stairs with the lamp oil in one hand and the logbook in the other.",
        "By the time she reached the lantern room the far shore had gone, and the bell on the harbour wall had started its slow count.",
        "Wren was waiting at the top, sitting on the step as if she had always been there.",
        "*Someone is always out there*, her mother used to say, and Mara had never found a way to argue with it.",
        "The wick took the flame on the second try.",
        "Below them the water moved like something breathing in its sleep.",
        "She wrote the time in the log, then the weather, then nothing at all for a long while.",
        "Wren said the ferry would be late, which was the kind of thing Wren said when she meant something else.",
        "It was **not** the first time the light had been needed after midnight.",
        "Salt had crusted the inside of the glass again, a white map of every storm that winter.",
        "Mara wiped a clear circle with her sleeve and looked out through it.",
        "There was a boat, or the shape of one, low and dark against the grey.",
        "Neither of them said anything until it turned toward the harbour mouth.",
        "The kettle in the keeper's room had boiled dry an hour ago, and nobody had noticed.",
        "Wren pulled her coat tighter and counted the seconds between the flashes out loud.",
        "Somewhere below, a door banged twice in the wind and then held.",
        "Mara thought of the letter on the kitchen table, still unopened, still *waiting*.",
        "The tide was turning; she could hear it in the change of the water against the rocks.",
        "When the boat was close enough to see its lamp, Wren stood up at last.",
    ];

    private static readonly string[] Lines =
    [
        "\"You don't have to stay,\" Mara said.",
        "\"I know,\" said Wren. \"I'm staying anyway.\"",
        "\"Is that them?\"",
        "\"It's someone.\"",
        "\"Teh light's steady now. Go and sleep.\"",
        "\"Tell me when you recieve the letter back.\"",
    ];

    /// <summary>Writes a project whose one scene holds the chapter, and returns the project's folder.</summary>
    public static string Write(string location)
    {
        var project = Project.Create(location, "The Long Watch");
        var random = new Random(2026);
        var text = new List<string> { "# The Long Watch" };
        while (Words.Count(string.Join("\n\n", text)) < Length)
        {
            if (text.Count % 30 == 0)
            {
                text.Add("---");
            }
            else if (text.Count % 7 == 3)
            {
                text.Add(Lines[random.Next(Lines.Length)]);
            }
            else if (text.Count % 23 == 11)
            {
                text.Add("> " + Sentences[random.Next(Sentences.Length)]);
            }
            else
            {
                text.Add(string.Join(' ', Enumerable.Range(0, random.Next(4, 9)).Select(_ => Sentences[random.Next(Sentences.Length)])));
            }
        }

        var scene = project.Scenes.Single();
        scene.Text = string.Join("\n\n", text) + "\n";
        project.Save(scene);
        return project.Folder;
    }
}
