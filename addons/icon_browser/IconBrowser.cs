#if TOOLS
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Godot;

[Tool]
public partial class IconBrowser : Control
{
    [Export]
    private LineEdit searchBar;

    [Export]
    private Button solidToggleBtn;

    [Export]
    private Button fetchIconsBtn;

    [Export]
    private Button prevPage;

    [Export]
    private Button nextPage;

    [Export]
    private FlowContainer iconsGrid;

    [Export]
    private PackedScene iconBtnScene;

    [Export]
    private FlowContainer selectedIcons;

    [Export]
    private PackedScene iconColourPickerScene;

    [Export]
    private LineEdit outputDirInput;

    [Export]
    private Button saveIconsBtn;

    private const uint MAX_ICONS_PER_PAGE = 50;

    private const string PLUGIN_ROOT = "addons/icon_browser";
    private const string ZIP_URI =
        "https://use.fontawesome.com/releases/v7.1.0/fontawesome-free-7.1.0-desktop.zip";
    private static string ZIP_NAME = ZIP_URI.Split("/").Last();
    private static string EXTRACT_DIR = $"{PLUGIN_ROOT}/{ZIP_NAME.TrimSuffix(".zip")}";
    private static string SVGS_PATH = $"{EXTRACT_DIR}/svgs-full";

    private const string ICONS_DIR = $"{PLUGIN_ROOT}/icons";
    private const string REGULAR_ICONS_DIRNAME = "regular";
    private const string SOLID_ICONS_DIRNAME = "solid";
    private const string LICENSE_NAME = "LICENSE.txt";

    private static System.Net.Http.HttpClient httpClient = new();

    private uint currentPage = 0;
    private uint totalSvgs = 0;
    private FileInfo[] svgs = [];
    private (Button, Action)[] buttons = [];

    private EditorFileSystem fs => EditorInterface.Singleton.GetResourceFilesystem();

    enum Page
    {
        Prev,
        Next,
    }

    public override void _Ready()
    {
        searchBar.TextSubmitted += (searchText) => SearchIcons(searchText, false);
        solidToggleBtn.Toggled += (_) => SearchIcons(searchBar.Text, false);
        fetchIconsBtn.Pressed += FetchIcons;
        prevPage.Pressed += () => Paginate(Page.Prev);
        nextPage.Pressed += () => Paginate(Page.Next);
        saveIconsBtn.Pressed += SaveIcons;

        if (!Directory.Exists(ICONS_DIR))
            FetchIcons();

        SearchIcons("", false);
    }

    public override void _ExitTree()
    {
        foreach ((Button btn, Action signal) in buttons)
            btn.Pressed -= signal;
    }

    private void FetchIcons()
    {
        if (Directory.Exists(EXTRACT_DIR))
            Directory.Delete(EXTRACT_DIR, true);

        Stream downloadStream = httpClient.GetStreamAsync(ZIP_URI).Result;
        FileStream fileStream = new FileStream(
            ZIP_NAME,
            FileMode.Create,
            System.IO.FileAccess.Write
        );

        downloadStream.CopyTo(fileStream);
        fileStream.Flush();
        fileStream.Close();

        ZipFile.ExtractToDirectory(ZIP_NAME, PLUGIN_ROOT);

        File.Delete(ZIP_NAME);

        if (Directory.Exists(ICONS_DIR))
            Directory.Delete(ICONS_DIR, true);

        Directory.CreateDirectory(ICONS_DIR);

        Directory.Move(
            $"{SVGS_PATH}/{REGULAR_ICONS_DIRNAME}",
            $"{ICONS_DIR}/{REGULAR_ICONS_DIRNAME}"
        );
        Directory.Move($"{SVGS_PATH}/{SOLID_ICONS_DIRNAME}", $"{ICONS_DIR}/{SOLID_ICONS_DIRNAME}");
        File.Copy($"{EXTRACT_DIR}/{LICENSE_NAME}", $"{ICONS_DIR}/{LICENSE_NAME}");

        Directory.Delete(EXTRACT_DIR, true);

        foreach (string dir in new string[] { SOLID_ICONS_DIRNAME, REGULAR_ICONS_DIRNAME })
        {
            DirectoryInfo d = new DirectoryInfo($"{ICONS_DIR}/{dir}");

            FileInfo[] svgs = d.GetFiles().Where((file) => file.Extension == ".svg").ToArray();
            foreach (FileInfo file in svgs)
            {
                ResizeSvg(file.FullName, 48, 48);
                RecolourSvg(file.FullName, Colors.White);
            }
        }

        fs.Scan();
    }

    private void SearchIcons(string searchText, bool fromPagination)
    {
        // we do this because pagination needs to keep track of the current page
        // but when triggering a search manually or toggling solid icons, we should
        // reset back to the first page
        if (!fromPagination)
            currentPage = 0;

        string dirname = solidToggleBtn.ButtonPressed ? SOLID_ICONS_DIRNAME : REGULAR_ICONS_DIRNAME;
        string svgDir = $"{ICONS_DIR}/{dirname}";

        DirectoryInfo d = new DirectoryInfo(svgDir);

        svgs = d.GetFiles()
            .Where<FileInfo>(
                (file) =>
                    file.Extension == ".svg"
                    && (searchText == "" ? true : file.Name.Contains(searchText))
            )
            .OrderBy((file) => file.Name)
            .ToArray();

        int start = (int)(MAX_ICONS_PER_PAGE * currentPage);
        int end = (int)(MAX_ICONS_PER_PAGE * (currentPage + 1));

        int len = svgs.Count();
        totalSvgs = (uint)len;

        if (len > start && len < end)
            svgs = svgs[start..len];
        else if (len >= end)
            svgs = svgs[start..end];

        foreach (Button btn in iconsGrid.GetChildren())
            btn.Free();

        foreach (FileInfo file in svgs)
            AddBtn(svgDir, file.Name);

        UpdatePaginationButtons();
    }

    private void Paginate(Page page)
    {
        int offset = page == Page.Prev ? -1 : 1;
        currentPage = (uint)((int)currentPage + offset);

        SearchIcons(searchBar.Text, true);
    }

    private void UpdatePaginationButtons()
    {
        if (totalSvgs <= MAX_ICONS_PER_PAGE)
        {
            prevPage.Disabled = true;
            nextPage.Disabled = true;
        }
        else if (currentPage == 0)
        {
            prevPage.Disabled = true;
            nextPage.Disabled = false;
        }
        else if (totalSvgs <= (MAX_ICONS_PER_PAGE * (currentPage + 1)))
        {
            prevPage.Disabled = false;
            nextPage.Disabled = true;
        }
        else
        {
            prevPage.Disabled = false;
            nextPage.Disabled = false;
        }
    }

    private void AddBtn(string dirPath, string fileName)
    {
        string iconName = fileName.TrimSuffix(".svg");
        Button btn = iconBtnScene.Instantiate<Button>();
        btn.Icon = GD.Load<Texture2D>($"res://{dirPath}/{fileName}");
        btn.TooltipText = iconName;

        iconsGrid.CallDeferred(MethodName.AddChild, btn);
        Action signal = () => SelectIcon(iconName, btn.Icon);
        btn.Pressed += signal;
        buttons.Append((btn, signal));
    }

    private void SelectIcon(string iconName, Texture2D icon)
    {
        IconColourPicker iconColourPicker = iconColourPickerScene.Instantiate<IconColourPicker>();
        iconColourPicker.IconButton.Icon = icon;
        iconColourPicker.IconButton.TooltipText = iconName;
        selectedIcons.CallDeferred(MethodName.AddChild, iconColourPicker);
    }

    private (XDocument, XElement, XNamespace) SvgRoot(string filePath)
    {
        StreamReader reader = new StreamReader(filePath);
        string contents = reader.ReadToEnd();
        XDocument svgDoc = XDocument.Parse(contents);
        XElement svg = svgDoc.Root;
        XNamespace ns = svg.Attribute("xmlns").Value;

        return (svgDoc, svg, ns);
    }

    private void ResizeSvg(string filePath, int width, int height)
    {
        (XDocument svgDoc, XElement svg, XNamespace ns) = SvgRoot(filePath);
        svg.SetAttributeValue("width", width.ToString());
        svg.SetAttributeValue("height", height.ToString());
        svgDoc.Save(filePath);
    }

    private void RecolourSvg(string filePath, Color colour)
    {
        (XDocument svgDoc, XElement svg, XNamespace ns) = SvgRoot(filePath);
        svg.Element(ns + "path").SetAttributeValue("fill", $"#{colour.ToHtml(false)}");
        svgDoc.Save(filePath);
    }

    private void SaveIcons()
    {
        string outputDir = outputDirInput.Text;
        if (outputDir == "")
            outputDir = ".";

        foreach (Control child in selectedIcons.GetChildren())
        {
            if (child is IconColourPicker picker)
            {
                string svgFile = $"{picker.IconButton.TooltipText}.svg";
                string dirname = solidToggleBtn.ButtonPressed
                    ? SOLID_ICONS_DIRNAME
                    : REGULAR_ICONS_DIRNAME;
                string svgOutputPath = $"{outputDir}/{picker.IconNameInput.Text}.svg";

                Directory.CreateDirectory(outputDir);
                File.Copy($"{ICONS_DIR}/{dirname}/{svgFile}", svgOutputPath, true);
                ResizeSvg(
                    svgOutputPath,
                    (int)picker.IconSizeInput.Value,
                    (int)picker.IconSizeInput.Value
                );
                RecolourSvg(svgOutputPath, picker.colourPickerBtn.Color);
            }

            child.QueueFree();
        }

        outputDirInput.Text = "";
        fs.Scan();
    }
}
#endif
