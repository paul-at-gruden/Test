using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using Sitecore.Globalization;

namespace GlobusWebsite.layouts
{
    public partial class BrowserLayout : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
          Uri uriCurrent = new Uri(Page.Request.Url.ToString());
          lblTest.Text = Translate.Text(uriCurrent.Host);

          Session["siteKey"] = Translate.Text(uriCurrent.Host);

        }
    }
}