using System.Drawing;
using System.IO;

using LiveSplit.Model;

namespace LiveSplit.UI.LayoutFactories;

public class StandardLayoutFactory : ILayoutFactory
{
    public ILayout Create(LiveSplitState state)
    {
        using var layout_stream = new MemoryStream(LiveSplit.Properties.Resources.DefaultLayout);
        ILayout layout = new XMLLayoutFactory(layout_stream).Create(state);

        layout.X = 100;
        layout.Y = 100;

        CenturyGothicFix(layout);

        return layout;
    }

    public static void CenturyGothicFix(ILayout layout)
    {
        if (layout.Settings.TimerFont.Name == "Century Gothic")
        {
            layout.Settings.TimerFont = new Font("Calibri", layout.Settings.TimerFont.Size, FontStyle.Bold, GraphicsUnit.Pixel);
        }
    }
}
