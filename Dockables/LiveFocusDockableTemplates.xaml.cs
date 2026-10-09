using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Cwseo.NINA.LiveFocus.Dockables
{
    [Export(typeof(ResourceDictionary))]
    public partial class LiveFocusDockableTemplates : ResourceDictionary
    {
        public LiveFocusDockableTemplates()
        {
            InitializeComponent();
        }
    }
}