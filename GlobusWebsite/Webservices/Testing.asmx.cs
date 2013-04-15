using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using GlobusWebsite.Classes.Tools;

namespace GlobusWebsite.Webservices
{
  /// <summary>
  /// Summary description for Testing
  /// </summary>
  [WebService(Namespace = "http://tempuri.org/")]
  [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
  [System.ComponentModel.ToolboxItem(false)]
  // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
  // [System.Web.Script.Services.ScriptService]
  public class Testing : System.Web.Services.WebService
  {

    [WebMethod]
    public string LoadXmlData()
    {
      string strMessage = "Ok";
      try
      {
        XmlDataImport xmlImp = new XmlDataImport();
        xmlImp.Run();
      }
      catch (Exception ex)
      {
        strMessage = ex.ToString();
      }

      return strMessage;
    }
  }
}
