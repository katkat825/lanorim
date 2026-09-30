using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Content.Tests
{
    // TEXT ON THE FRAME (cc_task_ui-issues-9-30.md 1.4): the theme's panels drew a 16 px frame (their
    // nine-slice texture margins) and started their content 12 px in, so text and buttons sat on the
    // border. Every StyleBoxTexture's content margin is at least its texture margin, on every side.
    // A content margin left out (-1) is Godot's "use the texture margin", which is fine
    public class ThemeMarginTests
    {
        const string Theme = "lanorim_theme.tres";

        static readonly string[] Sides = { "left", "top", "right", "bottom" };

        static IEnumerable<(string Id, Dictionary<string, float> Values)> TextureBoxes()
        {
            string text = File.ReadAllText(Theme);

            foreach (string block in Regex.Split(text, @"(?=^\[)", RegexOptions.Multiline))
            {
                Match head = Regex.Match(block, @"^\[sub_resource type=""StyleBoxTexture"" id=""(\w+)""\]");
                if (!head.Success) continue;

                var values = Regex.Matches(block, @"^(\w+_margin_\w+) = ([\d.]+)", RegexOptions.Multiline)
                                  .ToDictionary(m => m.Groups[1].Value,
                                                m => float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));

                yield return (head.Groups[1].Value, values);
            }
        }

        [Fact]
        public void NoContentStartsInsideItsFrame()
        {
            var boxes = TextureBoxes().ToList();
            Assert.Contains(boxes, b => b.Id == "Panel");
            Assert.Contains(boxes, b => b.Id == "Button");

            var inside = new List<string>();

            foreach ((string id, Dictionary<string, float> values) in boxes)
                foreach (string side in Sides)
                {
                    float frame = values.GetValueOrDefault("texture_margin_" + side);
                    if (!values.TryGetValue("content_margin_" + side, out float content)) continue;

                    if (content < frame) inside.Add($"{id} {side}: content {content} < frame {frame}");
                }

            Assert.True(inside.Count == 0, string.Join("\n", inside));
        }
    }
}
