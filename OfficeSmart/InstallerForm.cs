using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OfficeSmart;

public class InstallerForm : Form
{
	private static readonly Color BG = Color.FromArgb(14, 11, 26);

	private static readonly Color CARD = Color.FromArgb(22, 16, 43);

	private static readonly Color CARD2 = Color.FromArgb(32, 24, 58);

	private static readonly Color ACCENT = Color.FromArgb(124, 58, 237);

	private static readonly Color ACCENT2 = Color.FromArgb(157, 92, 246);

	private static readonly Color ADIM = Color.FromArgb(45, 31, 94);

	private static readonly Color TXT = Color.FromArgb(232, 224, 255);

	private static readonly Color MUTED = Color.FromArgb(123, 111, 160);

	private static readonly Color BORDER = Color.FromArgb(46, 32, 80);

	private static readonly Color LOGBG = Color.FromArgb(8, 6, 16);

	private static readonly Color LOGFG = Color.FromArgb(134, 239, 172);

	private static readonly Color WARNC = Color.FromArgb(251, 191, 36);

	private static readonly Color BLUEC = Color.FromArgb(59, 130, 246);

	private static readonly Color BLUEDIM = Color.FromArgb(20, 38, 70);

	private static readonly Color GREENC = Color.FromArgb(34, 197, 94);

	private static readonly Color REDC = Color.FromArgb(220, 38, 38);

	private static readonly Color REDDIM = Color.FromArgb(40, 12, 12);

	private const string ODT_URL = "https://download.microsoft.com/download/6c1eeb25-cf8b-41d9-8d0d-cc1dbc032140/officedeploymenttool_18827-20140.exe";

	private const string OFFLINE_PACKAGE_FOLDER = "OFFICE-OFFLINE";

	private const string UPDATE_MANIFEST_URL = "https://api.github.com/repos/JavierTorresFelendler/pcninja-smart-office-installer/contents/public-release/update-manifest.json?ref=main";

	private const string GITHUB_RELEASES_URL = "https://github.com/JavierTorresFelendler/pcninja-smart-office-installer/releases";

	private string odtExe = Path.Combine(Path.GetTempPath(), "pcninja_odt.exe");

	private string extractDir = Path.Combine(Path.GetTempPath(), "pcninja_odt_x");

	private string xmlFile = Path.Combine(Path.GetTempPath(), "pcninja_cfg.xml");

	private OfficeFamily family;

	private string ch365 = "Current";

	private bool addVisio;

	private bool addProject;

	private bool autoAct = true;

	private bool removeOld;

	private bool installDone;

	private bool offline;

	private string offPath;

	private bool use32bit;

	private bool updatePromptShown;

	private bool updateCheckInProgress;

	private UpdateManifest availableUpdateManifest;

	private Version availableUpdateVersion;

	private int step;

	private Dictionary<string, CheckBox> cbLTSCApps = new Dictionary<string, CheckBox>();

	private Dictionary<string, CheckBox> cb365Apps = new Dictionary<string, CheckBox>();

	private static readonly string[] LSTEPS = new string[5] { "Family", "Products", "Languages", "Advanced", "Install" };

	private static readonly string[] MSTEPS = new string[5] { "Family", "Channel", "Apps", "Languages", "Install" };

	private static readonly string[] LHINTS = new string[5] { "Choose your product family", "Select products and app components", "Select language packs", "Advanced installation options", "Review and install" };

	private static readonly string[] MHINTS = new string[5] { "Choose your product family", "Select an update channel", "Select apps to include", "Select language packs", "Review and install" };

	private static readonly string[] RSTEPS = new string[2] { "Family", "Confirm" };

	private static readonly string[] RHINTS = new string[2] { "Choose your product family", "Confirm removal of all Office products" };

	private static readonly string[] LPACKAGE_STEPS = new string[5] { "Family", "Products", "Languages", "Advanced", "Package" };

	private static readonly string[] LPACKAGE_HINTS = new string[5] { "Choose your product family", "Select products and app components", "Select language packs", "Advanced package options", "Review and create offline package" };

	private Panel pStepBar;

	private Panel pContent;

	private Panel pFooter;

	private Button btnBack;

	private Button btnNext;

	private Button btnUpdate;

	private Label lblHint;

	private Panel fCardLTSC;

	private Panel fCard365;

	private Panel fCardRemove;

	private Panel[] chCards = new Panel[3];

	private Dictionary<string, CheckBox> cbLTSCLangs = new Dictionary<string, CheckBox>();

	private Dictionary<string, CheckBox> cb365Langs = new Dictionary<string, CheckBox>();

	private Button btnToggleLTSC;

	private Button btnToggle365;

	private CheckBox chkVisio;

	private CheckBox chkProject;

	private CheckBox chkConfirmRemove;

	private CheckBox chkAutoAct;

	private CheckBox chkRemOld;

	private CheckBox chkOffline;

	private CheckBox chk32bit;

	private CheckBox chkVisio365;

	private CheckBox chkProject365;

	private TextBox txtOffPath;

	private RichTextBox txtLog;

	private ProgressBar pbar;

	private Label lblProg;

	private Label lblFamilyNote;

	private Panel sumPanel;

	private Panel pFamily;

	private Panel pLTSCProd;

	private Panel pLTSCLang;

	private Panel pLTSCAdv;

	private Panel pM365Ch;

	private Panel pM365Apps;

	private Panel pM365Lang;

	private Panel pInstall;

	private Panel[] ltscFlow;

	private Panel[] m365Flow;

	private Panel[] removeFlow;

	private BackgroundWorker worker;

	private const int FW = 740;

	private const int FH = 800;

	private const int HDR_H = 84;

	private const int STEP_H = 44;

	private const int FOOT_H = 60;

	private const int PAD = 22;

	private const int IW = 696;

	public InstallerForm()
	{
		offPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, OFFLINE_PACKAGE_FOLDER);
		_ = new string[9] { "Word", "Excel", "PowerPoint", "Outlook", "OneNote", "Access", "Publisher", "Teams", "OneDrive" };
		BuildForm();
		BuildWorker();
		Shown += delegate
		{
			BeginUpdateCheck(manual: false);
		};
	}

	private void BuildForm()
	{
		Text = "Smart Office Installer";
		base.ClientSize = new Size(740, 800);
		MinimumSize = new Size(756, 839);
		MaximumSize = MinimumSize;
		base.StartPosition = FormStartPosition.CenterScreen;
		BackColor = BG;
		ForeColor = TXT;
		Font = new Font("Segoe UI", 9.5f);
		base.FormBorderStyle = FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		TryLoadIcon();
		BuildHeader();
		BuildStepBar();
		BuildContentArea();
		BuildFooter();
		BuildStepPanels();
		Render();
	}

	private void BuildHeader()
	{
		Panel hdr = FlatPanel(0, 0, 740, 84, CARD);
		hdr.Paint += delegate(object s, PaintEventArgs e)
		{
			DrawBorderBottom(e.Graphics, hdr.Width, hdr.Height, BORDER);
		};
		Panel panel = new Panel
		{
			Location = new Point(16, 20),
			Size = new Size(44, 44),
			BackColor = ADIM
		};
		panel.Paint += delegate(object s, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (Pen pen = new Pen(ACCENT, 1.5f))
			{
				graphics.DrawRectangle(pen, 0, 0, 43, 43);
			}
			using Font font = new Font("Segoe UI", 14f, FontStyle.Bold);
			using SolidBrush brush = new SolidBrush(ACCENT2);
			string logoText = "JT";
			SizeF logoSize = graphics.MeasureString(logoText, font);
			graphics.DrawString(logoText, font, brush, (43f - logoSize.Width) / 2f, (43f - logoSize.Height) / 2f);
		};
		hdr.Controls.Add(panel);
		hdr.Controls.Add(new Label
		{
			Text = "Smart Office Installer",
			Location = new Point(72, 16),
			Size = new Size(500, 30),
			Font = new Font("Segoe UI", 14f, FontStyle.Bold),
			ForeColor = TXT,
			BackColor = Color.Transparent
		});
		hdr.Controls.Add(new Label
		{
			Text = "Volume Deployment Tool by PcNinja.Pro",
			Location = new Point(73, 50),
			Size = new Size(500, 20),
			Font = new Font("Segoe UI", 9f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		});
		btnUpdate = Btn("Check updates", new Point(584, 26), new Size(136, 30), primary: false);
		btnUpdate.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
		btnUpdate.Click += delegate
		{
			OnUpdateButtonClick();
		};
		hdr.Controls.Add(btnUpdate);
		base.Controls.Add(hdr);
	}

	private void BuildStepBar()
	{
		pStepBar = FlatPanel(0, 84, 740, 44, CARD);
		pStepBar.Paint += OnStepBarPaint;
		base.Controls.Add(pStepBar);
	}

	private void BuildContentArea()
	{
		int num = 128;
		int num2 = 800 - num - 60;
		pContent = new Panel
		{
			Location = new Point(0, num),
			Size = new Size(740, num2),
			BackColor = BG,
			AutoScroll = true
		};
		base.Controls.Add(pContent);
	}

	private void BuildFooter()
	{
		pFooter = FlatPanel(0, 740, 740, 60, CARD);
		pFooter.Paint += delegate(object s, PaintEventArgs e)
		{
			DrawBorderTop(e.Graphics, pFooter.Width, BORDER);
		};
		lblHint = new Label
		{
			Location = new Point(18, 20),
			Size = new Size(480, 20),
			Font = new Font("Segoe UI", 9f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		};
		pFooter.Controls.Add(lblHint);
		btnBack = Btn("← Back", new Point(508, 13), new Size(96, 34), primary: false);
		btnBack.Click += delegate
		{
			Nav(-1);
		};
		pFooter.Controls.Add(btnBack);
		btnNext = Btn("Next →", new Point(614, 13), new Size(112, 34), primary: true);
		btnNext.Click += delegate
		{
			Nav(1);
		};
		pFooter.Controls.Add(btnNext);
		base.Controls.Add(pFooter);
	}

	private void BuildStepPanels()
	{
		pFamily = BuildFamilyPanel();
		pLTSCProd = BuildLTSCProductsPanel();
		pLTSCLang = BuildLangPanel(cbLTSCLangs);
		pLTSCAdv = BuildAdvancedPanel();
		pM365Ch = BuildChannelPanel();
		pM365Apps = BuildM365AppsPanel();
		pM365Lang = BuildLangPanel(cb365Langs);
		pInstall = BuildInstallPanel();
		ltscFlow = new Panel[5] { pFamily, pLTSCProd, pLTSCLang, pLTSCAdv, pInstall };
		m365Flow = new Panel[5] { pFamily, pM365Ch, pM365Apps, pM365Lang, pInstall };
		removeFlow = new Panel[2] { pFamily, pInstall };
		Panel[] array = new Panel[8] { pFamily, pLTSCProd, pLTSCLang, pLTSCAdv, pM365Ch, pM365Apps, pM365Lang, pInstall };
		foreach (Panel panel in array)
		{
			panel.Visible = false;
			panel.BackColor = BG;
			pContent.Controls.Add(panel);
		}
	}

	private void OnStepBarPaint(object s, PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		string[] array = StepLabels();
		int num = array.Length;
		int num2 = pStepBar.Width / num;
		DrawBorderBottom(graphics, pStepBar.Width, pStepBar.Height, BORDER);
		for (int i = 0; i < num; i++)
		{
			int num3 = i * num2;
			bool flag = i == step;
			bool flag2 = i < step;
			if (flag)
			{
				using Pen pen = new Pen((family == OfficeFamily.Remove) ? REDC : ACCENT2, 2f);
				graphics.DrawLine(pen, num3 + 2, 42, num3 + num2 - 2, 42);
			}
			int num4 = num3 + 16;
			int num5 = 22;
			Color color = (flag2 ? ADIM : (flag ? ACCENT : CARD2));
			Color color2 = (flag2 ? ACCENT : (flag ? ACCENT2 : BORDER));
			using (SolidBrush brush = new SolidBrush(color))
			{
				graphics.FillEllipse(brush, num4 - 9, num5 - 9, 18, 18);
			}
			using (Pen pen2 = new Pen(color2, 1f))
			{
				graphics.DrawEllipse(pen2, num4 - 9, num5 - 9, 18, 18);
			}
			string s2 = (flag2 ? "v" : (i + 1).ToString());
			using (Font font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
			{
				using SolidBrush brush2 = new SolidBrush(flag2 ? ACCENT2 : (flag ? Color.White : MUTED));
				SizeF sizeF = graphics.MeasureString(s2, font);
				graphics.DrawString(s2, font, brush2, (float)num4 - sizeF.Width / 2f, (float)num5 - sizeF.Height / 2f);
			}
			using Font font2 = new Font("Segoe UI", flag ? 8.5f : 8f, flag ? FontStyle.Bold : FontStyle.Regular);
			using SolidBrush brush3 = new SolidBrush(flag ? TXT : MUTED);
			graphics.DrawString(array[i], font2, brush3, num3 + 30, 16f);
		}
	}

	private Panel[] Flow()
	{
		if (family != OfficeFamily.LTSC)
		{
			if (family != OfficeFamily.M365)
			{
				return removeFlow;
			}
			return m365Flow;
		}
		return ltscFlow;
	}

	private string[] StepLabels()
	{
		if (family == OfficeFamily.LTSC && offline)
		{
			return LPACKAGE_STEPS;
		}
		return (family == OfficeFamily.LTSC) ? LSTEPS : ((family == OfficeFamily.M365) ? MSTEPS : RSTEPS);
	}

	private string[] StepHints()
	{
		if (family == OfficeFamily.LTSC && offline)
		{
			return LPACKAGE_HINTS;
		}
		return (family == OfficeFamily.LTSC) ? LHINTS : ((family == OfficeFamily.M365) ? MHINTS : RHINTS);
	}

	private void Render()
	{
		Panel[] array = Flow();
		string[] array2 = StepHints();
		foreach (Control control in pContent.Controls)
		{
			control.Visible = false;
		}
		Panel obj = array[step];
		obj.Location = new Point(0, 0);
		obj.Size = new Size(740, pContent.Height);
		obj.Visible = true;
		pStepBar.Invalidate();
		lblHint.Text = "Step " + (step + 1) + " of " + array.Length + " - " + array2[step];
		btnBack.Visible = step > 0;
		bool flag = step == array.Length - 1;
		btnNext.Text = ((!flag) ? "Next" : ((family == OfficeFamily.Remove) ? "Remove Office" : ((family == OfficeFamily.LTSC && offline) ? "Create Package" : "Install Office")));
		btnNext.Size = (flag ? new Size(132, 34) : new Size(112, 34));
		btnNext.Location = (flag ? new Point(594, 13) : new Point(614, 13));
		btnNext.BackColor = (flag ? Color.FromArgb(150, 58, 237) : ACCENT);
		if (flag)
		{
			UpdateSummary();
		}
		RefreshNext();
	}

	private void Nav(int dir)
	{
		if (installDone)
		{
			if (dir == -1)
			{
				installDone = false;
				step = 0;
				Render();
			}
			if (dir == 1)
			{
				Application.Exit();
			}
			return;
		}
		Panel[] array = Flow();
		bool flag = step == array.Length - 1;
		if (dir == 1 && flag)
		{
			StartInstall();
			return;
		}
		int num = step + dir;
		if (num >= 0 && num < array.Length)
		{
			step = num;
			Render();
		}
	}

	private void RefreshNext()
	{
		bool flag = true;
		if (family == OfficeFamily.Remove)
		{
			if (step == 1)
			{
				flag = chkConfirmRemove != null && chkConfirmRemove.Checked;
			}
		}
		else if (family == OfficeFamily.LTSC)
		{
			if (step == 1)
			{
				flag = false;
				foreach (CheckBox value in cbLTSCApps.Values)
				{
					if (value.Checked)
					{
						flag = true;
						break;
					}
				}
				if (!flag && addVisio)
				{
					flag = true;
				}
				if (!flag && addProject)
				{
					flag = true;
				}
			}
			if (step == 2)
			{
				flag = false;
				foreach (CheckBox value2 in cbLTSCLangs.Values)
				{
					if (value2.Checked)
					{
						flag = true;
						break;
					}
				}
			}
		}
		else
		{
			if (step == 2)
			{
				flag = false;
				foreach (CheckBox value3 in cb365Apps.Values)
				{
					if (value3.Checked)
					{
						flag = true;
						break;
					}
				}
				if (!flag && addVisio)
				{
					flag = true;
				}
				if (!flag && addProject)
				{
					flag = true;
				}
			}
			if (step == 3)
			{
				flag = false;
				foreach (CheckBox value4 in cb365Langs.Values)
				{
					if (value4.Checked)
					{
						flag = true;
						break;
					}
				}
			}
		}
		btnNext.Enabled = flag;
		btnNext.BackColor = (flag ? ACCENT : ADIM);
		btnNext.ForeColor = (flag ? Color.White : MUTED);
	}

	private Panel BuildFamilyPanel()
	{
		Panel panel = ScrollPanel();
		int num = 24;
		panel.Controls.Add(SectionLabel("Choose your product family", 22, num));
		num += 30;
		fCardLTSC = BuildFamilyCard(OfficeFamily.LTSC);
		fCardLTSC.Location = new Point(22, num);
		fCardLTSC.Click += delegate
		{
			SelectFamily(OfficeFamily.LTSC);
		};
		foreach (Control control in fCardLTSC.Controls)
		{
			control.Click += delegate
			{
				SelectFamily(OfficeFamily.LTSC);
			};
		}
		panel.Controls.Add(fCardLTSC);
		fCard365 = BuildFamilyCard(OfficeFamily.M365);
		fCard365.Location = new Point(378, num);
		fCard365.Click += delegate
		{
			SelectFamily(OfficeFamily.M365);
		};
		foreach (Control control2 in fCard365.Controls)
		{
			control2.Click += delegate
			{
				SelectFamily(OfficeFamily.M365);
			};
		}
		panel.Controls.Add(fCard365);
		num += 168;
		fCardRemove = new Panel
		{
			Location = new Point(22, num),
			Size = new Size(696, 52),
			BackColor = REDDIM,
			Cursor = Cursors.Hand
		};
		fCardRemove.Paint += delegate(object s, PaintEventArgs e)
		{
			bool flag = family == OfficeFamily.Remove;
			using Pen pen = new Pen(flag ? REDC : BORDER, flag ? 1.5f : 1f);
			e.Graphics.DrawRectangle(pen, 0, 0, fCardRemove.Width - 1, fCardRemove.Height - 1);
		};
		fCardRemove.Controls.Add(new Label
		{
			Text = "Remove Office",
			Location = new Point(14, 8),
			Size = new Size(240, 20),
			Font = new Font("Segoe UI", 10f, FontStyle.Bold),
			ForeColor = REDC,
			BackColor = Color.Transparent
		});
		fCardRemove.Controls.Add(new Label
		{
			Text = "Completely uninstall all Office products from this machine.",
			Location = new Point(14, 28),
			Size = new Size(536, 18),
			Font = new Font("Segoe UI", 8.5f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		});
		fCardRemove.Controls.Add(Tag("Destructive", REDDIM, REDC, new Point(566, 16)));
		EventHandler value = delegate
		{
			SelectFamily(OfficeFamily.Remove);
		};
		fCardRemove.Click += value;
		foreach (Control control3 in fCardRemove.Controls)
		{
			control3.Click += value;
		}
		panel.Controls.Add(fCardRemove);
		num += 64;
		lblFamilyNote = new Label
		{
			Text = "Office 2024 LTSC selected — supports offline deployment and volume activation.",
			Location = new Point(22, num),
			Size = new Size(696, 22),
			Font = new Font("Segoe UI", 9f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		};
		panel.Controls.Add(lblFamilyNote);
		int num2 = 200;
		int num3 = 22;
		int num4 = num2 + 8 + num3;
		int num5 = num + 22;
		int num6 = 612;
		int num7 = num5 + (num6 - num5 - num4) / 2;
		int num8 = 22 + (696 - num2) / 2;
		PictureBox pictureBox = new PictureBox
		{
			Location = new Point(num8, num7),
			Size = new Size(num2, num2),
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.Transparent,
			Cursor = Cursors.Hand
		};
		pictureBox.Click += delegate
		{
			Process.Start(new ProcessStartInfo("https://www.PcNinja.Pro")
			{
				UseShellExecute = true
			});
		};
		try
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OfficeSmart.Ninja-DMT.png");
			if (stream != null)
			{
				pictureBox.Image = new Bitmap(stream);
			}
		}
		catch
		{
		}
		panel.Controls.Add(pictureBox);
		LinkLabel linkLabel = new LinkLabel
		{
			Text = "www.PcNinja.Pro",
			Location = new Point(22, num7 + num2 + 8),
			Size = new Size(696, num3),
			TextAlign = ContentAlignment.MiddleCenter,
			Font = new Font("Segoe UI", 10f, FontStyle.Bold),
			BackColor = Color.Transparent,
			LinkColor = ACCENT2,
			ActiveLinkColor = Color.White,
			VisitedLinkColor = ACCENT2
		};
		linkLabel.LinkClicked += delegate
		{
			Process.Start(new ProcessStartInfo("https://www.PcNinja.Pro")
			{
				UseShellExecute = true
			});
		};
		panel.Controls.Add(linkLabel);
		UpdateFamilyCards();
		return panel;
	}

	private Panel BuildFamilyCard(OfficeFamily which)
	{
		bool isLTSC = which == OfficeFamily.LTSC;
		int num = 340;
		Panel card = new Panel
		{
			Size = new Size(num, 154),
			Cursor = Cursors.Hand
		};
		card.BackColor = (isLTSC ? ADIM : CARD);
		card.Paint += delegate(object s, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			bool flag = (isLTSC ? (family == OfficeFamily.LTSC) : (family == OfficeFamily.M365));
			using Pen pen = new Pen((!flag) ? BORDER : (isLTSC ? ACCENT2 : BLUEC), flag ? 1.5f : 1f);
			graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
		};
		card.Controls.Add(new Label
		{
			Text = (isLTSC ? "Office 2024 LTSC" : "Microsoft 365"),
			Location = new Point(14, 16),
			Size = new Size(num - 20, 24),
			Font = new Font("Segoe UI", 11f, FontStyle.Bold),
			ForeColor = TXT,
			BackColor = Color.Transparent
		});
		card.Controls.Add(new Label
		{
			Text = (isLTSC ? "Volume License  ·  One-time install" : "Subscription  ·  Always up-to-date"),
			Location = new Point(14, 46),
			Size = new Size(num - 20, 18),
			Font = new Font("Segoe UI", 8.5f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		});
		card.Controls.Add(new Label
		{
			Text = (isLTSC ? "Offline deployable  ·  Auto-activation" : "Cloud-connected  ·  Online install only"),
			Location = new Point(14, 64),
			Size = new Size(num - 20, 18),
			Font = new Font("Segoe UI", 8.5f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		});
		card.Controls.Add(new Label
		{
			Text = (isLTSC ? "  Volume License  " : "  Subscription  "),
			Location = new Point(14, 96),
			AutoSize = true,
			Font = new Font("Segoe UI", 8f, FontStyle.Bold),
			ForeColor = (isLTSC ? ACCENT2 : BLUEC),
			BackColor = (isLTSC ? ADIM : BLUEDIM)
		});
		return card;
	}

	private void SelectFamily(OfficeFamily f)
	{
		family = f;
		step = 0;
		UpdateFamilyCards();
		Render();
	}

	private void UpdateFamilyCards()
	{
		fCardLTSC.BackColor = ((family == OfficeFamily.LTSC) ? ADIM : CARD);
		fCard365.BackColor = ((family == OfficeFamily.M365) ? BLUEDIM : CARD);
		fCardLTSC.Invalidate();
		fCard365.Invalidate();
		if (fCardRemove != null)
		{
			fCardRemove.Invalidate();
		}
		if (lblFamilyNote != null)
		{
			if (family == OfficeFamily.LTSC)
			{
				lblFamilyNote.Text = "Office 2024 LTSC selected — supports offline deployment and volume activation.";
			}
			else if (family == OfficeFamily.M365)
			{
				lblFamilyNote.Text = "Microsoft 365 selected — subscription plan, online install only. No offline mode.";
			}
			else
			{
				lblFamilyNote.Text = "Remove Office selected — this will uninstall all Office products from this machine.";
			}
		}
	}

	private Panel BuildLTSCProductsPanel()
	{
		Panel panel = ScrollPanel();
		int num = 24;
		panel.Controls.Add(SectionLabel("Select products and components", 22, num));
		num += 30;
		int num2 = 218;
		Panel panel2 = Card(22, num, 696, num2);
		panel.Controls.Add(panel2);
		panel2.Controls.Add(new Label
		{
			Text = "Office 2024 Professional Plus",
			Location = new Point(12, 12),
			Size = new Size(340, 22),
			Font = new Font("Segoe UI", 10f, FontStyle.Bold),
			ForeColor = TXT,
			BackColor = Color.Transparent
		});
		panel2.Controls.Add(Tag("Required", ADIM, ACCENT2, new Point(360, 14)));
		btnToggleLTSC = MakeToggleBtn("Deselect All", new Point(544, 8));
		panel2.Controls.Add(btnToggleLTSC);
		panel2.Controls.Add(new Panel
		{
			Location = new Point(12, 40),
			Size = new Size(672, 1),
			BackColor = BORDER
		});
		panel2.Controls.Add(SectionLabel("Included apps — uncheck to exclude", 12, 50));
		string[] array = new string[9] { "Word", "Excel", "PowerPoint", "Outlook", "OneNote", "Access", "Publisher", "Teams", "OneDrive" };
		bool[] array2 = new bool[9] { true, true, true, true, true, false, false, false, false };
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i];
			bool flag = array2[i];
			CheckBox checkBox = new CheckBox
			{
				Text = text,
				Checked = flag,
				Location = new Point(12 + i % 2 * 330, 74 + i / 2 * 26),
				Size = new Size(320, 22),
				ForeColor = (flag ? TXT : MUTED),
				BackColor = Color.Transparent,
				Font = new Font("Segoe UI", 9.5f)
			};
			checkBox.Tag = text;
			checkBox.CheckedChanged += delegate(object s, EventArgs ev)
			{
				CheckBox obj = (CheckBox)s;
				obj.ForeColor = (obj.Checked ? TXT : MUTED);
				UpdateToggleLTSC();
				RefreshNext();
			};
			panel2.Controls.Add(checkBox);
			cbLTSCApps[text] = checkBox;
		}
		btnToggleLTSC.Click += delegate
		{
			bool flag2 = false;
			foreach (CheckBox value in cbLTSCApps.Values)
			{
				if (value.Checked)
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2 && chkVisio != null && chkVisio.Checked)
			{
				flag2 = true;
			}
			if (!flag2 && chkProject != null && chkProject.Checked)
			{
				flag2 = true;
			}
			bool flag3 = !flag2;
			foreach (KeyValuePair<string, CheckBox> cbLTSCApp in cbLTSCApps)
			{
				cbLTSCApp.Value.Checked = flag3;
				cbLTSCApp.Value.ForeColor = (flag3 ? TXT : MUTED);
			}
			if (chkVisio != null)
			{
				chkVisio.Checked = flag3;
				addVisio = flag3;
			}
			if (chkProject != null)
			{
				chkProject.Checked = flag3;
				addProject = flag3;
			}
			UpdateToggleLTSC();
			RefreshNext();
		};
		num += num2 + 12;
		Panel panel3 = Card(22, num, 696, 46);
		chkVisio = new CheckBox
		{
			Text = "Visio 2024 Professional",
			Location = new Point(12, 12),
			Size = new Size(280, 22),
			ForeColor = TXT,
			BackColor = Color.Transparent,
			Font = new Font("Segoe UI", 9.5f)
		};
		chkVisio.CheckedChanged += delegate
		{
			addVisio = chkVisio.Checked;
			UpdateToggleLTSC();
			RefreshNext();
		};
		panel3.Controls.Add(chkVisio);
		panel3.Controls.Add(Tag("Optional", CARD2, MUTED, new Point(298, 14)));
		panel.Controls.Add(panel3);
		num += 58;
		Panel panel4 = Card(22, num, 696, 46);
		chkProject = new CheckBox
		{
			Text = "Project 2024 Professional",
			Location = new Point(12, 12),
			Size = new Size(280, 22),
			ForeColor = TXT,
			BackColor = Color.Transparent,
			Font = new Font("Segoe UI", 9.5f)
		};
		chkProject.CheckedChanged += delegate
		{
			addProject = chkProject.Checked;
			UpdateToggleLTSC();
			RefreshNext();
		};
		panel4.Controls.Add(chkProject);
		panel4.Controls.Add(Tag("Optional", CARD2, MUTED, new Point(298, 14)));
		panel.Controls.Add(panel4);
		num += 58;
		panel.Controls.Add(WarnLabel("  At least one product (Office app, Visio, or Project) must be selected to continue", 22, num));
		return panel;
	}

	private void UpdateToggleLTSC()
	{
		if (btnToggleLTSC == null)
		{
			return;
		}
		bool flag = false;
		foreach (CheckBox value in cbLTSCApps.Values)
		{
			if (value.Checked)
			{
				flag = true;
				break;
			}
		}
		if (!flag && chkVisio != null && chkVisio.Checked)
		{
			flag = true;
		}
		if (!flag && chkProject != null && chkProject.Checked)
		{
			flag = true;
		}
		btnToggleLTSC.Text = (flag ? "Deselect All" : "Select All");
	}

	private Panel BuildLangPanel(Dictionary<string, CheckBox> dict)
	{
		Panel panel = ScrollPanel();
		int num = 24;
		panel.Controls.Add(SectionLabel("Language packs", 22, num));
		num += 30;
		Panel panel2 = Card(22, num, 696, 10);
		panel.Controls.Add(panel2);
		int num2 = 14;
		string[] array = new string[2] { "en-us", "he-il" };
		string[] array2 = new string[2] { "English  (en-us)", "Hebrew  (he-il)" };
		string[] array3 = array;
		foreach (string text in array3)
		{
			CheckBox checkBox = new CheckBox
			{
				Text = ((text == "en-us") ? array2[0] : array2[1]),
				Checked = true,
				Location = new Point(14, num2),
				Size = new Size(420, 22),
				ForeColor = TXT,
				BackColor = Color.Transparent,
				Font = new Font("Segoe UI", 9.5f)
			};
			checkBox.Tag = text;
			checkBox.CheckedChanged += delegate
			{
				RefreshNext();
			};
			panel2.Controls.Add(checkBox);
			dict[text] = checkBox;
			num2 += 28;
		}
		panel2.Controls.Add(new Panel
		{
			Location = new Point(14, num2),
			Size = new Size(668, 1),
			BackColor = BORDER
		});
		num2 += 14;
		panel2.Controls.Add(SectionLabel("Additional languages (optional)", 14, num2));
		num2 += 24;
		string[] array4 = new string[8] { "ar-sa", "fr-fr", "de-de", "es-es", "ru-ru", "tr-tr", "zh-cn", "ja-jp" };
		string[] array5 = new string[8] { "Arabic (ar-sa)", "French (fr-fr)", "German (de-de)", "Spanish (es-es)", "Russian (ru-ru)", "Turkish (tr-tr)", "Chinese (zh-cn)", "Japanese (ja-jp)" };
		for (int num3 = 0; num3 < array4.Length; num3++)
		{
			string text2 = array4[num3];
			string text3 = array5[num3];
			CheckBox checkBox2 = new CheckBox
			{
				Text = text3,
				Checked = false,
				Location = new Point(14 + num3 % 2 * 340, num2 + num3 / 2 * 26),
				Size = new Size(320, 22),
				ForeColor = MUTED,
				BackColor = Color.Transparent,
				Font = new Font("Segoe UI", 9.5f)
			};
			checkBox2.Tag = text2;
			checkBox2.CheckedChanged += delegate(object s, EventArgs ev)
			{
				CheckBox obj = (CheckBox)s;
				obj.ForeColor = (obj.Checked ? TXT : MUTED);
				RefreshNext();
			};
			panel2.Controls.Add(checkBox2);
			dict[text2] = checkBox2;
		}
		int num4 = (panel2.Height = num2 + (array4.Length + 1) / 2 * 26 + 16);
		num += num4 + 10;
		panel.Controls.Add(WarnLabel("  At least one language must be selected to continue", 22, num));
		return panel;
	}

	private Panel BuildAdvancedPanel()
	{
		Panel panel = ScrollPanel();
		int num = 24;
		panel.Controls.Add(SectionLabel("Advanced installation options", 22, num));
		num += 30;
		Panel panel2 = Card(22, num, 696, 10);
		panel.Controls.Add(panel2);
		int num2 = 14;
		chkAutoAct = Chk("Auto-activate volume license during installation", 14, num2, chk: true);
		chkAutoAct.CheckedChanged += delegate
		{
			autoAct = chkAutoAct.Checked;
		};
		panel2.Controls.Add(chkAutoAct);
		num2 += 32;
		chkRemOld = Chk("Remove previous Office on target machine before installing", 14, num2, chk: false);
		chkRemOld.CheckedChanged += delegate
		{
			removeOld = chkRemOld.Checked;
		};
		panel2.Controls.Add(chkRemOld);
		num2 += 32;
		panel2.Controls.Add(new Panel
		{
			Location = new Point(14, num2),
			Size = new Size(668, 1),
			BackColor = BORDER
		});
		num2 += 16;
		chkOffline = Chk("Create offline package only - do not install on this PC", 14, num2, chk: false);
		chkOffline.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
		panel2.Controls.Add(chkOffline);
		panel2.Controls.Add(Tag("Advanced", Color.FromArgb(26, 15, 60), Color.FromArgb(167, 139, 250), new Point(220, num2 + 2)));
		num2 += 32;
		Panel offSub = new Panel
		{
			Location = new Point(28, num2),
			Size = new Size(656, 72),
			BackColor = CARD2
		};
		panel2.Controls.Add(offSub);
		offSub.Controls.Add(new Label
		{
			Text = "Creates OFFICE-OFFLINE there with Data, setup.exe, Office_Config.xml, and Install-Office.bat.",
			Location = new Point(10, 6),
			Size = new Size(636, 18),
			Font = new Font("Segoe UI", 8.5f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		});
		txtOffPath = new TextBox
		{
			Text = offPath,
			Location = new Point(10, 30),
			Size = new Size(530, 22),
			BackColor = CARD,
			ForeColor = MUTED,
			BorderStyle = BorderStyle.FixedSingle,
			Font = new Font("Consolas", 8.5f)
		};
		txtOffPath.TextChanged += delegate
		{
			offPath = txtOffPath.Text;
		};
		offSub.Controls.Add(txtOffPath);
		Button button = Btn("Browse", new Point(548, 28), new Size(74, 26), primary: false);
		button.Click += delegate
		{
			FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
			{
				Description = "Select where to create the OFFICE-OFFLINE folder"
			};
			if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
			{
				offPath = folderBrowserDialog.SelectedPath;
				txtOffPath.Text = offPath;
			}
		};
		offSub.Controls.Add(button);
		chkOffline.CheckedChanged += delegate
		{
			offline = chkOffline.Checked;
			offSub.BackColor = (offline ? CARD2 : BG);
			pStepBar.Invalidate();
		};
		num2 += 84;
		panel2.Controls.Add(new Panel
		{
			Location = new Point(14, num2),
			Size = new Size(668, 1),
			BackColor = BORDER
		});
		num2 += 16;
		panel2.Controls.Add(SectionLabel("Architecture", 14, num2));
		num2 += 22;
		chk32bit = Chk("64-bit (recommended)  —  uncheck only if 32-bit legacy plugins are required", 14, num2, chk: true);
		chk32bit.CheckedChanged += delegate
		{
			use32bit = !chk32bit.Checked;
		};
		panel2.Controls.Add(chk32bit);
		num2 += 28;
		panel2.Controls.Add(new Label
		{
			Text = "Using 32-bit Office on modern systems is not recommended.",
			Location = new Point(36, num2),
			Size = new Size(646, 18),
			Font = new Font("Segoe UI", 8.5f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		});
		num2 += 24;
		panel2.Height = num2 + 10;
		panel.Controls.Add(new Label
		{
			Text = "Online mode installs on this PC. Offline package mode only creates OFFICE-OFFLINE.",
			Location = new Point(22, num + panel2.Height + 10),
			Size = new Size(696, 20),
			Font = new Font("Segoe UI", 8.5f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		});
		return panel;
	}

	private Panel BuildChannelPanel()
	{
		Panel panel = ScrollPanel();
		int num = 24;
		panel.Controls.Add(SectionLabel("Select update channel", 22, num));
		num += 30;
		string[] cIds = new string[3] { "Current", "MonthlyEnterprise", "SemiAnnual" };
		string[] array = new string[3] { "Current Channel", "Monthly Enterprise Channel", "Semi-Annual Enterprise Channel" };
		string[] array2 = new string[3] { "Latest features", "Recommended", "Most stable" };
		string[] array3 = new string[3] { "New features as soon as ready. Updated monthly or more often. Ideal for early adopters.", "New features on a predictable monthly schedule. Best choice for most organisations.", "Updates twice a year only. Best for regulated or locked-down enterprise environments." };
		for (int i = 0; i < cIds.Length; i++)
		{
			string text = cIds[i];
			int idx = i;
			Panel panel2 = Card(22, num, 696, 74);
			panel2.Cursor = Cursors.Hand;
			panel2.BackColor = ((text == ch365) ? ADIM : CARD);
			chCards[i] = panel2;
			panel2.Controls.Add(new Label
			{
				Text = array[i],
				Location = new Point(14, 12),
				Size = new Size(340, 22),
				Font = new Font("Segoe UI", 10f, FontStyle.Bold),
				ForeColor = TXT,
				BackColor = Color.Transparent
			});
			panel2.Controls.Add(Tag(array2[i], ADIM, ACCENT2, new Point(364, 14)));
			panel2.Controls.Add(new Label
			{
				Text = array3[i],
				Location = new Point(14, 38),
				Size = new Size(668, 18),
				Font = new Font("Segoe UI", 8.5f),
				ForeColor = MUTED,
				BackColor = Color.Transparent
			});
			EventHandler value = delegate
			{
				SelectChannel(cIds[idx]);
			};
			panel2.Click += value;
			foreach (Control control in panel2.Controls)
			{
				control.Click += value;
			}
			panel.Controls.Add(panel2);
			num += 86;
		}
		panel.Controls.Add(new Label
		{
			Text = "All channels install the same Microsoft 365 apps — the difference is update frequency only.",
			Location = new Point(22, num),
			Size = new Size(696, 20),
			Font = new Font("Segoe UI", 8.5f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		});
		return panel;
	}

	private void SelectChannel(string cid)
	{
		ch365 = cid;
		string[] array = new string[3] { "Current", "MonthlyEnterprise", "SemiAnnual" };
		for (int i = 0; i < chCards.Length; i++)
		{
			if (chCards[i] != null)
			{
				chCards[i].BackColor = ((array[i] == cid) ? ADIM : CARD);
			}
		}
	}

	private Panel BuildM365AppsPanel()
	{
		Panel panel = ScrollPanel();
		int num = 24;
		panel.Controls.Add(SectionLabel("Select products and apps", 22, num));
		num += 30;
		string[] array = new string[9] { "Word", "Excel", "PowerPoint", "Outlook", "OneNote", "Access", "Publisher", "Teams", "OneDrive" };
		bool[] array2 = new bool[9] { true, true, true, true, true, false, false, false, false };
		int num2 = 218;
		Panel panel2 = Card(22, num, 696, num2);
		panel.Controls.Add(panel2);
		panel2.Controls.Add(new Label
		{
			Text = "Microsoft 365 Apps for Enterprise",
			Location = new Point(14, 12),
			Size = new Size(340, 22),
			Font = new Font("Segoe UI", 10f, FontStyle.Bold),
			ForeColor = TXT,
			BackColor = Color.Transparent
		});
		panel2.Controls.Add(Tag("Required", ADIM, ACCENT2, new Point(364, 14)));
		btnToggle365 = MakeToggleBtn("Deselect All", new Point(544, 8));
		panel2.Controls.Add(btnToggle365);
		panel2.Controls.Add(new Panel
		{
			Location = new Point(14, 40),
			Size = new Size(668, 1),
			BackColor = BORDER
		});
		panel2.Controls.Add(SectionLabel("Included apps — uncheck to exclude", 14, 50));
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i];
			bool flag = array2[i];
			CheckBox checkBox = new CheckBox
			{
				Text = text,
				Checked = flag,
				Location = new Point(14 + i % 2 * 330, 74 + i / 2 * 26),
				Size = new Size(320, 22),
				ForeColor = (flag ? TXT : MUTED),
				BackColor = Color.Transparent,
				Font = new Font("Segoe UI", 9.5f)
			};
			checkBox.Tag = text;
			checkBox.CheckedChanged += delegate(object s, EventArgs ev)
			{
				CheckBox obj = (CheckBox)s;
				obj.ForeColor = (obj.Checked ? TXT : MUTED);
				UpdateToggle365();
				RefreshNext();
			};
			panel2.Controls.Add(checkBox);
			cb365Apps[text] = checkBox;
		}
		btnToggle365.Click += delegate
		{
			bool flag2 = false;
			foreach (CheckBox value in cb365Apps.Values)
			{
				if (value.Checked)
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2 && chkVisio365 != null && chkVisio365.Checked)
			{
				flag2 = true;
			}
			if (!flag2 && chkProject365 != null && chkProject365.Checked)
			{
				flag2 = true;
			}
			bool flag3 = !flag2;
			foreach (KeyValuePair<string, CheckBox> cb365App in cb365Apps)
			{
				cb365App.Value.Checked = flag3;
				cb365App.Value.ForeColor = (flag3 ? TXT : MUTED);
			}
			if (chkVisio365 != null)
			{
				chkVisio365.Checked = flag3;
				addVisio = flag3;
			}
			if (chkProject365 != null)
			{
				chkProject365.Checked = flag3;
				addProject = flag3;
			}
			UpdateToggle365();
			RefreshNext();
		};
		num += num2 + 12;
		Panel panel3 = Card(22, num, 696, 46);
		chkVisio365 = new CheckBox
		{
			Text = "Visio for Microsoft 365",
			Location = new Point(12, 12),
			Size = new Size(360, 22),
			ForeColor = TXT,
			BackColor = Color.Transparent,
			Font = new Font("Segoe UI", 9.5f)
		};
		chkVisio365.CheckedChanged += delegate
		{
			addVisio = chkVisio365.Checked;
			UpdateToggle365();
			RefreshNext();
		};
		panel3.Controls.Add(chkVisio365);
		panel3.Controls.Add(Tag("Optional", CARD2, MUTED, new Point(230, 14)));
		panel.Controls.Add(panel3);
		num += 58;
		Panel panel4 = Card(22, num, 696, 46);
		chkProject365 = new CheckBox
		{
			Text = "Project for Microsoft 365",
			Location = new Point(12, 12),
			Size = new Size(360, 22),
			ForeColor = TXT,
			BackColor = Color.Transparent,
			Font = new Font("Segoe UI", 9.5f)
		};
		chkProject365.CheckedChanged += delegate
		{
			addProject = chkProject365.Checked;
			UpdateToggle365();
			RefreshNext();
		};
		panel4.Controls.Add(chkProject365);
		panel4.Controls.Add(Tag("Optional", CARD2, MUTED, new Point(230, 14)));
		panel.Controls.Add(panel4);
		num += 58;
		panel.Controls.Add(WarnLabel("  At least one product (app, Visio, or Project) must be selected to continue", 22, num));
		return panel;
	}

	private void UpdateToggle365()
	{
		if (btnToggle365 == null)
		{
			return;
		}
		bool flag = false;
		foreach (CheckBox value in cb365Apps.Values)
		{
			if (value.Checked)
			{
				flag = true;
				break;
			}
		}
		if (!flag && chkVisio365 != null && chkVisio365.Checked)
		{
			flag = true;
		}
		if (!flag && chkProject365 != null && chkProject365.Checked)
		{
			flag = true;
		}
		btnToggle365.Text = (flag ? "Deselect All" : "Select All");
	}

	private Panel BuildInstallPanel()
	{
		Panel panel = ScrollPanel();
		int num = 24;
		panel.Controls.Add(SectionLabel("Installation summary", 22, num));
		num += 30;
		sumPanel = Card(22, num, 696, 108);
		panel.Controls.Add(sumPanel);
		string[] array = new string[4] { "Family", "Products", "Languages", "Mode" };
		for (int i = 0; i < array.Length; i++)
		{
			sumPanel.Controls.Add(new Label
			{
				Text = array[i].ToUpper(),
				Name = "sk_" + array[i],
				Location = new Point(14 + i % 2 * 340, 12 + i / 2 * 46),
				Size = new Size(200, 14),
				Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
				ForeColor = MUTED,
				BackColor = Color.Transparent
			});
			sumPanel.Controls.Add(new Label
			{
				Text = "—",
				Name = "sv_" + array[i],
				Location = new Point(14 + i % 2 * 340, 28 + i / 2 * 46),
				Size = new Size(320, 18),
				Font = new Font("Segoe UI", 9.5f),
				ForeColor = TXT,
				BackColor = Color.Transparent
			});
		}
		num += 120;
		chkConfirmRemove = new CheckBox
		{
			Text = "I understand this will permanently remove ALL Office products from this machine.",
			Location = new Point(22, num),
			Size = new Size(696, 22),
			ForeColor = REDC,
			BackColor = Color.Transparent,
			Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
			Visible = false
		};
		chkConfirmRemove.CheckedChanged += delegate
		{
			RefreshNext();
		};
		panel.Controls.Add(chkConfirmRemove);
		num += 32;
		panel.Controls.Add(SectionLabel("Installation log", 22, num));
		num += 28;
		txtLog = new RichTextBox
		{
			Location = new Point(22, num),
			Size = new Size(696, 168),
			BackColor = LOGBG,
			ForeColor = LOGFG,
			Font = new Font("Consolas", 9f),
			BorderStyle = BorderStyle.None,
			ReadOnly = true,
			ScrollBars = RichTextBoxScrollBars.Vertical
		};
		panel.Controls.Add(txtLog);
		num += 180;
		lblProg = new Label
		{
			Text = "Ready.",
			Location = new Point(22, num),
			Size = new Size(696, 18),
			Font = new Font("Segoe UI", 8.5f),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		};
		panel.Controls.Add(lblProg);
		num += 20;
		pbar = new ProgressBar
		{
			Location = new Point(22, num),
			Size = new Size(696, 6),
			Minimum = 0,
			Maximum = 100,
			Value = 0,
			Style = ProgressBarStyle.Continuous
		};
		panel.Controls.Add(pbar);
		return panel;
	}

	private void UpdateSummary()
	{
		if (family == OfficeFamily.Remove)
		{
			SetSV("Family", "Remove Office");
			SetSV("Products", "All Office products");
			SetSV("Languages", "N/A");
			SetSV("Mode", "Full removal");
			if (chkConfirmRemove != null)
			{
				chkConfirmRemove.Visible = true;
				chkConfirmRemove.Checked = false;
			}
			return;
		}
		if (chkConfirmRemove != null)
		{
			chkConfirmRemove.Visible = false;
		}
		SetSV("Family", (family == OfficeFamily.LTSC) ? "Office 2024 LTSC" : "Microsoft 365");
		if (family == OfficeFamily.LTSC)
		{
			string text = "Office ProPlus 2024";
			if (addVisio)
			{
				text += "  +  Visio";
			}
			if (addProject)
			{
				text += "  +  Project";
			}
			SetSV("Products", text);
			string text2 = (offline ? "Create offline package only - no local install" : "Install on this PC");
			if (removeOld)
			{
				text2 += " | Remove previous Office on target";
			}
			if (autoAct)
			{
				text2 += " | Auto-activate";
			}
			SetSV("Mode", text2);
		}
		else
		{
			string text3 = "M365 Apps for Enterprise";
			if (addVisio)
			{
				text3 += "  +  Visio";
			}
			if (addProject)
			{
				text3 += "  +  Project";
			}
			SetSV("Products", text3);
			SetSV("Mode", "Online  ·  Channel: " + ch365);
		}
		Dictionary<string, CheckBox> obj = ((family == OfficeFamily.LTSC) ? cbLTSCLangs : cb365Langs);
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, CheckBox> item in obj)
		{
			if (item.Value.Checked)
			{
				list.Add(item.Key);
			}
		}
		SetSV("Languages", string.Join(", ", list.ToArray()));
	}

	private void SetSV(string key, string val)
	{
		foreach (Control control in sumPanel.Controls)
		{
			if (control.Name == "sv_" + key)
			{
				control.Text = val;
				break;
			}
		}
	}

	private void BuildWorker()
	{
		worker = new BackgroundWorker
		{
			WorkerReportsProgress = true
		};
		worker.DoWork += OnWork;
		worker.ProgressChanged += OnProgress;
		worker.RunWorkerCompleted += OnDone;
	}

	private void StartInstall()
	{
		if (!worker.IsBusy)
		{
			btnNext.Enabled = false;
			btnBack.Enabled = false;
			txtLog.Clear();
			pbar.Value = 0;
			AppLog("Smart Office Installer - starting...");
			worker.RunWorkerAsync();
		}
	}

	private void OnWork(object sender, DoWorkEventArgs e)
	{
		BackgroundWorker bw = (BackgroundWorker)sender;
		try
		{
			if (family == OfficeFamily.Remove)
			{
				RunRemove(bw);
			}
			else if (offline && family == OfficeFamily.LTSC)
			{
				RunOfflineDownload(bw);
			}
			else
			{
				RunInstall(bw);
			}
		}
		catch (Exception ex)
		{
			Rep(bw, -1, "ERROR: " + ex.Message);
		}
	}

	private void DownloadAndExtractODT(BackgroundWorker bw, int dlPct, int exPct)
	{
		Rep(bw, dlPct, "Downloading Office Deployment Tool...");
		using (WebClient webClient = new WebClient())
		{
			webClient.DownloadFile("https://download.microsoft.com/download/6c1eeb25-cf8b-41d9-8d0d-cc1dbc032140/officedeploymenttool_18827-20140.exe", odtExe);
		}
		Rep(bw, exPct, "Extracting ODT...");
		if (Directory.Exists(extractDir))
		{
			Directory.Delete(extractDir, recursive: true);
		}
		Directory.CreateDirectory(extractDir);
		Process process = Process.Start(new ProcessStartInfo
		{
			FileName = odtExe,
			Arguments = "/quiet /extract:\"" + extractDir + "\"",
			UseShellExecute = false,
			CreateNoWindow = true
		});
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new Exception("ODT extraction failed (exit code " + process.ExitCode + ").");
		}
	}

	private void RunRemove(BackgroundWorker bw)
	{
		DownloadAndExtractODT(bw, 10, 35);
		Rep(bw, 50, "Removing Office — this may take several minutes...");
		string contents = "<Configuration>\r\n  <Remove All=\"True\" />\r\n  <Display Level=\"Full\" AcceptEULA=\"TRUE\" />\r\n</Configuration>";
		File.WriteAllText(xmlFile, contents, Encoding.UTF8);
		Process process = Process.Start(new ProcessStartInfo
		{
			FileName = Path.Combine(extractDir, "setup.exe"),
			Arguments = "/configure \"" + xmlFile + "\"",
			UseShellExecute = false,
			CreateNoWindow = true
		});
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new Exception("Office removal failed (exit code " + process.ExitCode + ").");
		}
		Rep(bw, 95, "Cleaning up...");
		TryDel(odtExe);
		TryDel(xmlFile);
		TryDelDir(extractDir);
		Rep(bw, 100, "Office has been removed successfully.");
	}

	private void RunInstall(BackgroundWorker bw)
	{
		DownloadAndExtractODT(bw, 5, 30);
		Rep(bw, 46, "Writing XML configuration...");
		File.WriteAllText(xmlFile, BuildXML(), Encoding.UTF8);
		Rep(bw, 50, "XML ready.");
		Rep(bw, 52, "Starting Office installation (10-30 minutes)...");
		Process process = Process.Start(new ProcessStartInfo
		{
			FileName = Path.Combine(extractDir, "setup.exe"),
			Arguments = "/configure \"" + xmlFile + "\"",
			UseShellExecute = false,
			CreateNoWindow = true
		});
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new Exception("Office setup failed (exit code " + process.ExitCode + ").");
		}
		Rep(bw, 92, "Installation complete.");
		Rep(bw, 94, "Cleaning up...");
		TryDel(odtExe);
		TryDel(xmlFile);
		TryDelDir(extractDir);
		Rep(bw, 100, "All done! Office installed successfully.");
	}

	private void RunOfflineDownload(BackgroundWorker bw)
	{
		string selectedPath = (offPath ?? "").Trim();
		if (string.IsNullOrWhiteSpace(selectedPath))
		{
			Rep(bw, -1, "Offline download cancelled - no folder selected.");
			return;
		}

		selectedPath = Path.GetFullPath(selectedPath);
		string packagePath = ResolveOfflinePackagePath(selectedPath);
		offPath = selectedPath;
		if (txtOffPath != null && txtOffPath.IsHandleCreated)
		{
			Invoke((MethodInvoker)delegate
			{
				txtOffPath.Text = offPath;
			});
		}
		if (!Directory.Exists(packagePath))
		{
			Directory.CreateDirectory(packagePath);
		}

		string stagingRoot = Path.Combine(packagePath, "_OfficeSmartDownload");
		TryDelDir(stagingRoot);
		Directory.CreateDirectory(stagingRoot);

		DownloadAndExtractODT(bw, 5, 30);
		Rep(bw, 46, "Writing XML configuration...");
		File.WriteAllText(xmlFile, BuildXML(), Encoding.UTF8);
		string text = Path.Combine(stagingRoot, "_dl.xml");
		File.WriteAllText(text, BuildXML(stagingRoot), Encoding.UTF8);
		Rep(bw, 52, "Downloading Office source files (20-40 min)...");
		Process process = Process.Start(new ProcessStartInfo
		{
			FileName = Path.Combine(extractDir, "setup.exe"),
			Arguments = "/download \"" + text + "\"",
			WorkingDirectory = stagingRoot,
			UseShellExecute = false,
			CreateNoWindow = true
		});
		MonitorOfflineDownloadProgress(bw, process, stagingRoot);
		TryDel(text);
		if (process.ExitCode != 0)
		{
			throw new Exception("Office source download failed (exit code " + process.ExitCode + ").");
		}
		string text2 = Path.Combine(stagingRoot, "Office");
		if (!Directory.Exists(text2))
		{
			throw new Exception("Office source folder was not created by the download process.");
		}
		TryDelDir(Path.Combine(packagePath, "Data"));
		CopyDirectoryContents(text2, packagePath);
		File.Copy(Path.Combine(extractDir, "setup.exe"), Path.Combine(packagePath, "setup.exe"), overwrite: true);
		File.Copy(xmlFile, Path.Combine(packagePath, "Office_Config.xml"), overwrite: true);
		string contents = "@echo off\r\nnet session >nul 2>&1\r\nif %errorLevel% neq 0 (\r\n    echo.\r\n    echo  ERROR: Must be run as Administrator.\r\n    echo  Right-click Install-Office.bat and choose Run as administrator.\r\n    echo.\r\n    pause\r\n    exit /b 1\r\n)\r\necho  Smart Office Installer - Offline Mode\r\necho.\r\ncd /d \"%~dp0\"\r\necho  Starting installation, please wait...\r\nsetup.exe /configure Office_Config.xml\r\necho.\r\necho  Done. Press any key to exit.\r\npause > nul\r\n";
		File.WriteAllText(Path.Combine(packagePath, "Install-Office.bat"), contents, Encoding.ASCII);
		Rep(bw, 96, "Cleaning up temporary files...");
		TryDel(odtExe);
		TryDel(xmlFile);
		TryDelDir(extractDir);
		TryDelDir(stagingRoot);
		Rep(bw, 100, "Done!  Copy the entire \"" + packagePath + "\" folder to the target machine\r\nand run Install-Office.bat as Administrator.");
	}

	private void MonitorOfflineDownloadProgress(BackgroundWorker bw, Process process, string stagingRoot)
	{
		string officeRoot = Path.Combine(stagingRoot, "Office");
		DateTime started = DateTime.UtcNow;
		DateTime lastReport = DateTime.MinValue;
		long lastBytes = 0L;
		DateTime lastSample = started;
		int progress = 52;
		while (!process.WaitForExit(1000))
		{
			long bytes = GetDirectorySizeSafe(officeRoot);
			DateTime now = DateTime.UtcNow;
			double elapsedMinutes = Math.Max(0.1, (now - started).TotalMinutes);
			double sampleMinutes = Math.Max(0.1, (now - lastSample).TotalMinutes);
			double totalMb = bytes / 1048576.0;
			double mbPerMinute = Math.Max(0.0, ((bytes - lastBytes) / 1048576.0) / sampleMinutes);
			if (bytes > lastBytes || (now - lastReport).TotalSeconds >= 10.0)
			{
				int activityProgress = 52 + Math.Min(36, (int)Math.Floor(elapsedMinutes * 1.5));
				if (bytes > lastBytes)
				{
					activityProgress = Math.Max(activityProgress, progress + 1);
				}
				progress = Math.Min(88, Math.Max(progress, activityProgress));
				Rep(bw, progress, string.Format("Downloading Office source files... {0:N0} MB downloaded ({1:N1} MB/min)", totalMb, mbPerMinute));
				lastReport = now;
				lastBytes = bytes;
				lastSample = now;
			}
		}
	}

	private string ResolveOfflinePackagePath(string selectedPath)
	{
		string trimmedPath = selectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string folderName = Path.GetFileName(trimmedPath);
		if (string.Equals(folderName, OFFLINE_PACKAGE_FOLDER, StringComparison.OrdinalIgnoreCase))
		{
			return selectedPath;
		}
		return Path.Combine(selectedPath, OFFLINE_PACKAGE_FOLDER);
	}

	private string BuildXML()
	{
		return BuildXML(null);
	}

	private string BuildXML(string sourcePath)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = (use32bit ? "32" : "64");
		string text2 = ((family == OfficeFamily.LTSC) ? "PerpetualVL2024" : ch365);
		string text3 = string.IsNullOrWhiteSpace(sourcePath) ? "" : " SourcePath=\"" + SecurityElement.Escape(sourcePath) + "\"";
		stringBuilder.AppendLine("<Configuration>");
		stringBuilder.AppendLine("  <Add" + text3 + " OfficeClientEdition=\"" + text + "\" Channel=\"" + text2 + "\">");
		if (family == OfficeFamily.LTSC)
		{
			stringBuilder.AppendLine("    <Product ID=\"ProPlus2024Volume\">");
			AddLangs(stringBuilder, cbLTSCLangs);
			stringBuilder.AppendLine("      <ExcludeApp ID=\"Lync\" />");
			foreach (KeyValuePair<string, CheckBox> cbLTSCApp in cbLTSCApps)
			{
				if (!cbLTSCApp.Value.Checked)
				{
					stringBuilder.AppendLine("      <ExcludeApp ID=\"" + cbLTSCApp.Key + "\" />");
				}
			}
			stringBuilder.AppendLine("    </Product>");
			if (addVisio)
			{
				stringBuilder.AppendLine("    <Product ID=\"VisioPro2024Volume\">");
				AddLangs(stringBuilder, cbLTSCLangs);
				stringBuilder.AppendLine("    </Product>");
			}
			if (addProject)
			{
				stringBuilder.AppendLine("    <Product ID=\"ProjectPro2024Volume\">");
				AddLangs(stringBuilder, cbLTSCLangs);
				stringBuilder.AppendLine("    </Product>");
			}
		}
		else
		{
			stringBuilder.AppendLine("    <Product ID=\"O365ProPlusRetail\">");
			AddLangs(stringBuilder, cb365Langs);
			stringBuilder.AppendLine("      <ExcludeApp ID=\"Lync\" />");
			foreach (KeyValuePair<string, CheckBox> cb365App in cb365Apps)
			{
				if (!cb365App.Value.Checked)
				{
					stringBuilder.AppendLine("      <ExcludeApp ID=\"" + cb365App.Key + "\" />");
				}
			}
			stringBuilder.AppendLine("    </Product>");
			if (addVisio)
			{
				stringBuilder.AppendLine("    <Product ID=\"VisioProRetail\">");
				AddLangs(stringBuilder, cb365Langs);
				stringBuilder.AppendLine("    </Product>");
			}
			if (addProject)
			{
				stringBuilder.AppendLine("    <Product ID=\"ProjectProRetail\">");
				AddLangs(stringBuilder, cb365Langs);
				stringBuilder.AppendLine("    </Product>");
			}
		}
		stringBuilder.AppendLine("  </Add>");
		if (removeOld)
		{
			stringBuilder.AppendLine("  <Remove All=\"True\" />");
		}
		stringBuilder.AppendLine("  <Display Level=\"Full\" AcceptEULA=\"TRUE\" />");
		if (family == OfficeFamily.LTSC && autoAct)
		{
			stringBuilder.AppendLine("  <Property Name=\"AUTOACTIVATE\" Value=\"1\" />");
		}
		stringBuilder.AppendLine("  <RemoveMSI All=\"True\" />");
		stringBuilder.AppendLine("</Configuration>");
		return stringBuilder.ToString();
	}

	private void AddLangs(StringBuilder sb, Dictionary<string, CheckBox> dict)
	{
		foreach (KeyValuePair<string, CheckBox> item in dict)
		{
			if (item.Value.Checked)
			{
				sb.AppendLine("      <Language ID=\"" + item.Key + "\" />");
			}
		}
	}

	private void OnProgress(object sender, ProgressChangedEventArgs e)
	{
		string text = e.UserState as string;
		if (e.ProgressPercentage >= 0)
		{
			pbar.Value = Math.Min(e.ProgressPercentage, 100);
		}
		if (text != null)
		{
			AppLog(text);
			lblProg.Text = text;
		}
	}

	private void OnDone(object sender, RunWorkerCompletedEventArgs e)
	{
		installDone = true;
		btnBack.Enabled = true;
		btnBack.Visible = true;
		btnBack.Text = "Back";
		btnBack.Size = new Size(96, 34);
		btnBack.Location = new Point(508, 13);
		btnBack.BackColor = CARD2;
		btnBack.ForeColor = MUTED;
		btnBack.FlatAppearance.BorderSize = 1;
		btnBack.FlatAppearance.BorderColor = BORDER;
		btnBack.FlatAppearance.MouseOverBackColor = CARD;
		btnNext.Enabled = true;
		btnNext.Text = "Exit";
		btnNext.Size = new Size(96, 34);
		btnNext.Location = new Point(614, 13);
		btnNext.BackColor = CARD2;
		btnNext.ForeColor = MUTED;
		btnNext.FlatAppearance.BorderSize = 1;
		btnNext.FlatAppearance.BorderColor = BORDER;
		btnNext.FlatAppearance.MouseOverBackColor = CARD;
		if (lblHint != null)
		{
			lblHint.Text = "Done  —  press Back to run again, or Exit to close";
		}
	}

	private void Rep(BackgroundWorker bw, int pct, string msg)
	{
		bw.ReportProgress(pct, msg);
	}

	private void AppLog(string msg)
	{
		string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "]  " + msg + "\n";
		if (txtLog.InvokeRequired)
		{
			txtLog.Invoke((MethodInvoker)delegate
			{
				txtLog.AppendText(line);
				txtLog.ScrollToCaret();
			});
		}
		else
		{
			txtLog.AppendText(line);
			txtLog.ScrollToCaret();
		}
	}

	private void TryDel(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private void TryDelDir(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch
		{
		}
	}

	private static long GetDirectorySizeSafe(string path)
	{
		try
		{
			if (!Directory.Exists(path))
			{
				return 0L;
			}
			long total = 0L;
			foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
			{
				try
				{
					total += new FileInfo(file).Length;
				}
				catch
				{
				}
			}
			return total;
		}
		catch
		{
			return 0L;
		}
	}

	private void CopyDirectoryContents(string sourceDir, string destDir)
	{
		if (!Directory.Exists(destDir))
		{
			Directory.CreateDirectory(destDir);
		}
		foreach (string directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
		{
			string targetDir = Path.Combine(destDir, directory.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			if (!Directory.Exists(targetDir))
			{
				Directory.CreateDirectory(targetDir);
			}
		}
		foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
		{
			string targetFile = Path.Combine(destDir, file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			string targetParent = Path.GetDirectoryName(targetFile);
			if (!Directory.Exists(targetParent))
			{
				Directory.CreateDirectory(targetParent);
			}
			File.Copy(file, targetFile, overwrite: true);
		}
	}

	private Panel ScrollPanel()
	{
		return new Panel
		{
			AutoScroll = true,
			BackColor = BG
		};
	}

	private Panel FlatPanel(int x, int y, int w, int h, Color bg)
	{
		return new Panel
		{
			Location = new Point(x, y),
			Size = new Size(w, h),
			BackColor = bg
		};
	}

	private Panel Card(int x, int y, int w, int h)
	{
		Panel p = new Panel
		{
			Location = new Point(x, y),
			Size = new Size(w, h),
			BackColor = CARD
		};
		p.Paint += delegate(object s, PaintEventArgs e)
		{
			using Pen pen = new Pen(BORDER, 1f);
			e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
		};
		return p;
	}

	private Button MakeToggleBtn(string text, Point loc)
	{
		Button button = new Button();
		button.Text = text;
		button.Location = loc;
		button.Size = new Size(138, 28);
		button.FlatStyle = FlatStyle.Flat;
		button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
		button.BackColor = ADIM;
		button.ForeColor = ACCENT2;
		button.Cursor = Cursors.Hand;
		button.TextAlign = ContentAlignment.MiddleCenter;
		button.UseVisualStyleBackColor = false;
		button.UseCompatibleTextRendering = true;
		button.FlatAppearance.BorderColor = ACCENT;
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.MouseOverBackColor = ACCENT;
		button.Paint += delegate(object s, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Button button2 = (Button)s;
			graphics.Clear(button2.BackColor);
			using (Pen pen = new Pen(ACCENT, 1f))
			{
				graphics.DrawRectangle(pen, 0, 0, button2.Width - 1, button2.Height - 1);
			}
			TextRenderer.DrawText(graphics, button2.Text, button2.Font, new Rectangle(0, 0, button2.Width, button2.Height), ACCENT2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		};
		return button;
	}

	private Button Btn(string text, Point loc, Size sz, bool primary)
	{
		Button button = new Button
		{
			Text = text,
			Location = loc,
			Size = sz,
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand,
			Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
		};
		if (primary)
		{
			button.BackColor = ACCENT;
			button.ForeColor = Color.White;
			button.FlatAppearance.BorderSize = 0;
			button.FlatAppearance.MouseOverBackColor = ACCENT2;
		}
		else
		{
			button.BackColor = CARD2;
			button.ForeColor = MUTED;
			button.FlatAppearance.BorderSize = 1;
			button.FlatAppearance.BorderColor = BORDER;
			button.FlatAppearance.MouseOverBackColor = CARD;
		}
		return button;
	}

	private CheckBox Chk(string text, int x, int y, bool chk)
	{
		return new CheckBox
		{
			Text = text,
			Checked = chk,
			Location = new Point(x, y),
			Size = new Size(666, 22),
			ForeColor = TXT,
			BackColor = Color.Transparent,
			Font = new Font("Segoe UI", 9.5f)
		};
	}

	private Label SectionLabel(string text, int x, int y)
	{
		return new Label
		{
			Text = text,
			Location = new Point(x, y),
			Size = new Size(696, 18),
			Font = new Font("Segoe UI", 8f, FontStyle.Bold),
			ForeColor = MUTED,
			BackColor = Color.Transparent
		};
	}

	private Label WarnLabel(string text, int x, int y)
	{
		return new Label
		{
			Text = text,
			Location = new Point(x, y),
			Size = new Size(696, 20),
			Font = new Font("Segoe UI", 8.5f),
			ForeColor = WARNC,
			BackColor = Color.Transparent
		};
	}

	private new Label Tag(string text, Color bg, Color fg, Point loc)
	{
		return new Label
		{
			Text = "  " + text + "  ",
			Location = loc,
			AutoSize = true,
			Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
			ForeColor = fg,
			BackColor = bg
		};
	}

	private void DrawBorderBottom(Graphics g, int w, int h, Color c)
	{
		using Pen pen = new Pen(c, 1f);
		g.DrawLine(pen, 0, h - 1, w, h - 1);
	}

	private void DrawBorderTop(Graphics g, int w, Color c)
	{
		using Pen pen = new Pen(c, 1f);
		g.DrawLine(pen, 0, 0, w, 0);
	}

	private void TryLoadIcon()
	{
		try
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OfficeSmart.favicon.ico");
			if (stream != null)
			{
				base.Icon = new Icon(stream);
			}
		}
		catch
		{
		}
	}

	private void OnUpdateButtonClick()
	{
		if (availableUpdateManifest != null)
		{
			OpenExternalUrl(GetUpdateUrl(availableUpdateManifest));
			return;
		}
		BeginUpdateCheck(manual: true);
	}

	private void BeginUpdateCheck(bool manual)
	{
		if (updateCheckInProgress)
		{
			return;
		}
		updateCheckInProgress = true;
		SetUpdateButtonChecking();
		Task.Run(delegate
		{
			return CheckForUpdateAsync(manual);
		});
	}

	private async Task CheckForUpdateAsync(bool manual)
	{
		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			string responseJson;
			using (WebClient webClient = new WebClient())
			{
				webClient.Headers[HttpRequestHeader.UserAgent] = "PcNinja-SmartOfficeInstaller";
				webClient.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
				responseJson = await webClient.DownloadStringTaskAsync(UPDATE_MANIFEST_URL);
			}

			string manifestJson = DecodeGitHubContentPayload(responseJson);
			UpdateManifest manifest = ReadJson<UpdateManifest>(manifestJson);
			if (!IsValidUpdateManifest(manifest))
			{
				CompleteUpdateCheckFailure(manual, "The update manifest is not valid.");
				return;
			}

			Version currentVersion = GetCurrentFileVersion();
			Version latestVersion;
			if (!Version.TryParse(manifest.Version, out latestVersion))
			{
				CompleteUpdateCheckFailure(manual, "The update version is not valid.");
				return;
			}
			if (IsDisposed || !IsHandleCreated)
			{
				return;
			}

			BeginInvoke((MethodInvoker)delegate
			{
				CompleteUpdateCheck(manual, manifest, currentVersion, latestVersion);
			});
		}
		catch (Exception ex)
		{
			CompleteUpdateCheckFailure(manual, ex.Message);
		}
	}

	private void CompleteUpdateCheck(bool manual, UpdateManifest manifest, Version currentVersion, Version latestVersion)
	{
		updateCheckInProgress = false;
		if (latestVersion.CompareTo(currentVersion) > 0)
		{
			availableUpdateManifest = manifest;
			availableUpdateVersion = latestVersion;
			SetUpdateButtonAvailable(manifest, latestVersion);
			ShowUpdatePrompt(manifest, currentVersion, latestVersion);
			return;
		}

		availableUpdateManifest = null;
		availableUpdateVersion = null;
		SetUpdateButtonUpToDate();
		if (manual)
		{
			MessageBox.Show(this, "You are running the latest version.", "Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
	}

	private void CompleteUpdateCheckFailure(bool manual, string message)
	{
		if (IsDisposed || !IsHandleCreated)
		{
			return;
		}
		BeginInvoke((MethodInvoker)delegate
		{
			updateCheckInProgress = false;
			SetUpdateButtonIdle();
			if (manual)
			{
				MessageBox.Show(this, "Could not check for updates.\r\n\r\n" + message, "Updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		});
	}

	private void SetUpdateButtonIdle()
	{
		SetUpdateButtonState("Check updates", CARD2, MUTED, BORDER, CARD);
	}

	private void SetUpdateButtonChecking()
	{
		SetUpdateButtonState("Checking...", CARD2, MUTED, BORDER, CARD2);
	}

	private void SetUpdateButtonAvailable(UpdateManifest manifest, Version latestVersion)
	{
		string label = string.IsNullOrWhiteSpace(manifest.PublicLabel) ? "v" + latestVersion : manifest.PublicLabel;
		SetUpdateButtonState("Update " + label, Color.FromArgb(61, 44, 12), WARNC, WARNC, Color.FromArgb(82, 61, 18));
	}

	private void SetUpdateButtonUpToDate()
	{
		SetUpdateButtonState("Up to date", Color.FromArgb(12, 48, 26), GREENC, GREENC, Color.FromArgb(15, 66, 34));
	}

	private void SetUpdateButtonState(string text, Color backColor, Color foreColor, Color borderColor, Color hoverColor)
	{
		if (btnUpdate == null)
		{
			return;
		}
		btnUpdate.Text = text;
		btnUpdate.Enabled = true;
		btnUpdate.BackColor = backColor;
		btnUpdate.ForeColor = foreColor;
		btnUpdate.FlatAppearance.BorderColor = borderColor;
		btnUpdate.FlatAppearance.BorderSize = 1;
		btnUpdate.FlatAppearance.MouseOverBackColor = hoverColor;
	}

	private void ShowUpdatePrompt(UpdateManifest manifest, Version currentVersion, Version latestVersion)
	{
		if (updatePromptShown || IsDisposed)
		{
			return;
		}
		updatePromptShown = true;

		string latestLabel = string.IsNullOrWhiteSpace(manifest.PublicLabel) ? "v" + latestVersion : manifest.PublicLabel;
		string currentLabel = currentVersion.ToString();
		string message = "A new version of Smart Office Installer is available." +
			"\r\n\r\nCurrent version: " + currentLabel +
			"\r\nLatest version: " + latestLabel +
			"\r\n\r\nOpen the GitHub download now?";

		DialogResult result = MessageBox.Show(this, message, "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
		if (result == DialogResult.Yes)
		{
			OpenExternalUrl(GetUpdateUrl(manifest));
		}
	}

	private static string DecodeGitHubContentPayload(string json)
	{
		GitHubContentResponse response;
		try
		{
			response = ReadJson<GitHubContentResponse>(json);
		}
		catch
		{
			return json;
		}

		if (response == null || string.IsNullOrWhiteSpace(response.Content) || !string.Equals(response.EncodingName, "base64", StringComparison.OrdinalIgnoreCase))
		{
			return json;
		}

		string encoded = RemoveWhitespace(response.Content);
		return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
	}

	private static T ReadJson<T>(string json) where T : class
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		using MemoryStream stream = new MemoryStream(bytes);
		return new DataContractJsonSerializer(typeof(T)).ReadObject(stream) as T;
	}

	private static string RemoveWhitespace(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}
		StringBuilder builder = new StringBuilder(value.Length);
		foreach (char c in value)
		{
			if (!char.IsWhiteSpace(c))
			{
				builder.Append(c);
			}
		}
		return builder.ToString();
	}

	private static bool IsValidUpdateManifest(UpdateManifest manifest)
	{
		if (manifest == null || manifest.Portable == null)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.Portable.FileName))
		{
			return false;
		}
		if (!IsHttpsUrl(manifest.Portable.Url))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(manifest.ReleaseNotesUrl) && !IsHttpsUrl(manifest.ReleaseNotesUrl))
		{
			return false;
		}
		string sha256 = (manifest.Portable.Sha256 ?? "").Trim();
		if (sha256.Length != 64)
		{
			return false;
		}
		foreach (char c in sha256)
		{
			if (!Uri.IsHexDigit(c))
			{
				return false;
			}
		}
		return true;
	}

	private static Version GetCurrentFileVersion()
	{
		string fileVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
		Version version;
		if (Version.TryParse(fileVersion, out version))
		{
			return version;
		}
		return new Version(0, 0, 0, 0);
	}

	private static string GetUpdateUrl(UpdateManifest manifest)
	{
		if (manifest != null && manifest.Portable != null && IsHttpsUrl(manifest.Portable.Url))
		{
			return manifest.Portable.Url;
		}
		if (manifest != null && IsHttpsUrl(manifest.ReleaseNotesUrl))
		{
			return manifest.ReleaseNotesUrl;
		}
		return GITHUB_RELEASES_URL;
	}

	private static bool IsHttpsUrl(string url)
	{
		Uri uri;
		return Uri.TryCreate(url, UriKind.Absolute, out uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
	}

	private static void OpenExternalUrl(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = url,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show("Could not open the update link.\r\n\r\n" + ex.Message, "Update link", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}

	[DataContract]
	private sealed class GitHubContentResponse
	{
		[DataMember(Name = "encoding")]
		public string EncodingName { get; set; }

		[DataMember(Name = "content")]
		public string Content { get; set; }
	}

	[DataContract]
	private sealed class UpdateManifest
	{
		[DataMember(Name = "channel")]
		public string Channel { get; set; }

		[DataMember(Name = "publicLabel")]
		public string PublicLabel { get; set; }

		[DataMember(Name = "version")]
		public string Version { get; set; }

		[DataMember(Name = "minimumSupportedVersion")]
		public string MinimumSupportedVersion { get; set; }

		[DataMember(Name = "releaseNotesUrl")]
		public string ReleaseNotesUrl { get; set; }

		[DataMember(Name = "portable")]
		public UpdatePackage Portable { get; set; }

		[DataMember(Name = "signing")]
		public UpdateSigning Signing { get; set; }
	}

	[DataContract]
	private sealed class UpdatePackage
	{
		[DataMember(Name = "fileName")]
		public string FileName { get; set; }

		[DataMember(Name = "url")]
		public string Url { get; set; }

		[DataMember(Name = "sha256")]
		public string Sha256 { get; set; }
	}

	[DataContract]
	private sealed class UpdateSigning
	{
		[DataMember(Name = "required")]
		public bool Required { get; set; }

		[DataMember(Name = "expectedPublisher")]
		public string ExpectedPublisher { get; set; }
	}
}
