using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DIsplayApp
{
	public static class Program
	{
		public static (Task, DisplayForm) Launch(Bitmap frame) 
		{
			var form = new DisplayForm(frame);
			return (Task.Run(() => form.ShowDialog()), form);
		}

		
		//static void Main(DisplayForm form)
		//{
		//	Application.SetHighDpiMode(HighDpiMode.SystemAware);
		//	Application.EnableVisualStyles();
		//	Application.SetCompatibleTextRenderingDefault(false);
		//	Application.Run(form);
		//}
	}
}
