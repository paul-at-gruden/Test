using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Publishing;
using Microsoft.XmlDiffPatch;

namespace GlobusWebsite.Classes.Tools
{
  public class XmlDataImport
  {
    private XmlDocument oDoc;
    private Boolean isValid = true;
    private static long lValidFiles = 0;
    private static long lInvalidFiles = 0;
    private Dictionary<string, string> dictInvalidFiles = new Dictionary<string, string>();
    
    private static Database dbMaster = Sitecore.Data.Database.GetDatabase("master");
    private static Database dbWeb = Sitecore.Data.Database.GetDatabase("web");
    private static DirectoryInfo inputDirectory = null;
    private static DirectoryInfo updateDirectory = null;
    private static DirectoryInfo currentDirectory = null;
    private static DirectoryInfo rejectDirectory = null;
    private static XmlDocument xmlDocToday = new XmlDocument();
    private static XmlDocument xmlCurrent = new XmlDocument();
    private static XmlDocument xmlDiffGraph = new XmlDocument();
    private static StreamReader strRead = null;

    private static PublishOptions pubOpts = new PublishOptions(dbMaster, dbWeb, PublishMode.Incremental, Sitecore.Globalization.Language.Predefined.English, System.DateTime.Now);

    private static ExtraNight oExtraNight = null;

    //Items
    private static Item itmTourRoot = dbMaster.GetItem(ConfigurationManager.AppSettings.Get("TourDataRoot"));
    private static Item itmTours = null;
    private static Item itmBrand = null;
    private static Item itmSeason = null;
    private static Item itmTourFolder = null;
    private static Item itmTour = null;
    private static Item itmItineraryFolder = null;
    private static Item itmItineraryDay = null;
    private static Item itmExtraNightsFolder = null;
    private static Item itmExtraNightsPreFolder = null;
    private static Item itmExtraNightsPostFolder = null;
    private static Item itmExtraNight = null;
    private static Item itmHotelsRoot = dbMaster.GetItem(string.Format("{0}/Hotels", ConfigurationManager.AppSettings.Get("TourDataRoot")));
    private static Item itmExcursionsRoot = dbMaster.GetItem(string.Format("{0}/Excursions", ConfigurationManager.AppSettings.Get("TourDataRoot")));

    //Templates
    private static TemplateItem tiFolderTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("FolderTemplate")));
    private static TemplateItem tiTourTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("TourTemplate")));
    private static TemplateItem tiItineraryDayTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("ItineraryDayTemplate")));
    private static TemplateItem tiDateRangeTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("DateRangeTemplate")));
    private static TemplateItem tiPriceTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("PriceTemplate")));
    private static TemplateItem tiDepartureTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("DepartureTemplate")));
    private static TemplateItem tiHotelListTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("HotelListTemplate")));
    private static TemplateItem tiHotelListItemTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("HotelListItemTemplate")));
    private static TemplateItem tiHotelDetailsTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("HotelDetailsTemplate")));
    private static TemplateItem tiOptionalChargeTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("OptionalChargeTemplate")));
    private static TemplateItem tiExcursionTemplate = dbMaster.GetTemplate(new ID(ConfigurationManager.AppSettings.Get("ExcursionTemplate")));

    private static List<String> lstHotels = new List<String>();
    private static List<String> lstExcursions = new List<String>();
    
    private Boolean isValidXml = true;

    public void Run()
    {
      Stream strmDiff = null;
      DateTime dtStart = DateTime.Now;
      int iFileCount = 0;
      
      try
      {
        
        string strXmlInputRoot = ConfigurationManager.AppSettings.Get("XmlInputDirectory");
        inputDirectory = new DirectoryInfo(string.Format("{0}\\today", strXmlInputRoot));
        updateDirectory = new DirectoryInfo(string.Format("{0}\\update", strXmlInputRoot));
        currentDirectory = new DirectoryInfo(string.Format("{0}\\current", strXmlInputRoot));
        rejectDirectory = new DirectoryInfo(string.Format("{0}\\reject", strXmlInputRoot));

        // delete all files in update directory
        updateDirectory.Empty();

        // get list of files in input & current directories
        FileInfo[] arrFiles = inputDirectory.GetFiles();
        
        arrFiles = (from oFile in arrFiles
                   orderby oFile.Length ascending
                   select oFile).ToArray<FileInfo>();
        string[] arrCurrentFiles = Directory.GetFiles(currentDirectory.FullName);
        writeLogInfo(string.Format("{0} files in today's folder", arrFiles.Length.ToString()));
        int iMaxFiles = arrFiles.Length;
        /*
        if (iMaxFiles > 100)
          iMaxFiles = 100;
        */
        for (int i = 0; i < iMaxFiles; i++)
        {
          FileInfo fiFile = arrFiles[i];
          iFileCount++;
          writeLogInfo(fiFile.Name + " : " + iFileCount.ToString());
          if (validateXml(fiFile))
          {
            xmlDocToday = oDoc;
            if (!arrCurrentFiles.Contains<string>(string.Format("{0}\\{1}", currentDirectory.FullName, fiFile.Name)))
            {
              if (processWholeFile(fiFile, xmlDocToday))
              {
                // Move file to "Current" and "Update" folders
                fiFile.CopyTo(string.Format("{0}\\{1}", updateDirectory.FullName, fiFile.Name));
                fiFile.MoveTo(string.Format("{0}\\{1}", currentDirectory.FullName, fiFile.Name));
              }
            }
            else
            {
              xmlCurrent.Load(string.Format("{0}\\{1}", currentDirectory.FullName, fiFile.Name));
              strmDiff = new MemoryStream();
              XmlTextWriter diffGram = new XmlTextWriter(new StreamWriter(strmDiff));
              diffGram.Formatting = Formatting.Indented;
              XmlDiff diff = new XmlDiff(XmlDiffOptions.IgnoreChildOrder);
              diff.Compare(xmlCurrent, xmlDocToday, diffGram);
              strmDiff.Position = 0;
              strRead = new StreamReader(strmDiff);
              string strDiffGraph = strRead.ReadToEnd();
              xmlDiffGraph.LoadXml(strDiffGraph);
              if (xmlDiffGraph.ChildNodes[1].HasChildNodes)
              {
                if (processWholeFile(fiFile, xmlDocToday))
                {
                  // Move file to "Current" and "Update" folders
                  fiFile.CopyTo(string.Format("{0}\\{1}", updateDirectory.FullName, fiFile.Name));
                  fiFile.MoveTo(string.Format("{0}\\{1}", currentDirectory.FullName, fiFile.Name));
                }
              }
            }
          }
          else
          {
            fiFile.MoveTo(string.Format("{0}\\{1}", rejectDirectory.FullName, fiFile.Name));
          }
          
        }
        //PublishTours(itmTourFolder);
      }
      catch (Exception ex)
      {
        logError(ex.ToString());
      }
      finally
      {
        // clean up resources
        if (strmDiff != null)
        {
          strmDiff.Close();
          strmDiff.Dispose();
        }
        if (strRead != null)
        {
          strRead.Close();
          strRead.Dispose();
        }
        writeLogInfo((DateTime.Now - dtStart).TotalSeconds.ToString());
      }
    }

    private Boolean validateXml(FileInfo fiFile)
    {
      isValid = true;
      string strDTDPath = "C:/Clients/Globus/GlobusWebsite/GlobusWebsite/XmlData/Tour.dtd";
        //HttpContext.Current.Server.MapPath("~/XmlData/Tour.dtd").Replace("\\", "/");

      try
      {

        oDoc = new XmlDocument();
        oDoc.Load(fiFile.FullName);
        oDoc.InsertBefore(oDoc.CreateDocumentType("Tour", null, String.Format("file:///{0}", strDTDPath), null), oDoc.DocumentElement);
        XmlReaderSettings settings = new XmlReaderSettings();
        //settings.ProhibitDtd = false;
        settings.DtdProcessing = DtdProcessing.Parse;
        settings.ValidationType = ValidationType.DTD;
        settings.ValidationEventHandler += new ValidationEventHandler(delegate(object sender, ValidationEventArgs args)
        {
          isValid = false;
          dictInvalidFiles.Add(fiFile.FullName.Substring(fiFile.FullName.LastIndexOf("\\") + 1), args.Message);
        });
        XmlReader validator = XmlReader.Create(new StringReader(oDoc.OuterXml), settings);
        while (validator.Read())
        {
        }
        validator.Close();

      }
      catch (Exception ex)
      {
        isValid = false;
      }

      lValidFiles += isValid ? 1 : 0;
      lInvalidFiles += isValid ? 0 : 1;

      return isValid;

    }

    static void vr_ValidationEventHandler(object sender, ValidationEventArgs e)
    {
      Console.WriteLine("***Validation error");
      Console.WriteLine("\tSeverity:{0}", e.Severity);
      Console.WriteLine("\tMessage  :{0}", e.Message);
    }

    private static Boolean processWholeFile(FileInfo fiFile, XmlDocument xmlDoc)
    {
      Boolean processedOk = true;
      Item itmCurrentTour = null;
      try
      {
        string strBrand = xmlDoc.SelectSingleNode("//Fields/Brand").InnerText;
        string strSeason = xmlDoc.SelectSingleNode("//Fields/Season").InnerText;
        string strProdCode = xmlDoc.SelectSingleNode("//Fields/ProdCode").InnerText;
        Dictionary<string, string> dictTourFields = new Dictionary<string, string>();
        dictTourFields.Add("Brand", strBrand);
        dictTourFields.Add("Season", strSeason);
        dictTourFields.Add("Product Code", strProdCode);
        dictTourFields.Add("Name", xmlDoc.SelectSingleNode("//Fields/Name").InnerText);
        dictTourFields.Add("Tour Style", xmlDoc.SelectSingleNode("//Fields/TourStyle").InnerText);
        dictTourFields.Add("Duration", xmlDoc.SelectSingleNode("//Fields/Duration").InnerText);
        dictTourFields.Add("Start City", xmlDoc.SelectSingleNode("//Fields/StartCity").InnerText);
        dictTourFields.Add("End City", xmlDoc.SelectSingleNode("//Fields/EndCity").InnerText);
        dictTourFields.Add("Breakfast", xmlDoc.SelectSingleNode("//Fields/Breakfast").InnerText);
        dictTourFields.Add("Lunch", xmlDoc.SelectSingleNode("//Fields/Lunch").InnerText);
        dictTourFields.Add("Dinner", xmlDoc.SelectSingleNode("//Fields/Dinner").InnerText);
        dictTourFields.Add("Features", xmlDoc.SelectSingleNode("//Fields/Features").InnerText);
        itmCurrentTour = processTour(strSeason, strBrand, strProdCode, dictTourFields);

        List<ItineraryDay> lstItineraryDays = new List<ItineraryDay>();
        XmlNodeList nlItineraryDays = xmlDoc.SelectNodes("/Tour/Itinerary/ItineraryDay");
        foreach (XmlNode ndDay in nlItineraryDays)
        {
          ItineraryDay newDay = new ItineraryDay();
          newDay.dayNumber = int.Parse(ndDay["DayNo"].InnerText);
          newDay.description = ndDay["Description"].InnerText;
          newDay.program = ndDay["Program"].InnerText;
          lstItineraryDays.Add(newDay);
        }
        processItineraryItems(itmCurrentTour, lstItineraryDays);

        
        XmlNodeList nlExtraNights = xmlDoc.SelectNodes("/Tour/ExtraNights/ExtraNight");
        if (nlExtraNights.Count > 0)
        {
          foreach (XmlNode ndExtraNight in nlExtraNights)
          {
            foreach(XmlNode ndDateRange in (ndExtraNight["DateRanges"]).SelectNodes("//DateRange"))
            {
              itmExtraNightsFolder = getOrCreateItem("Extra Nights", tiFolderTemplate, itmCurrentTour);
              itmExtraNightsPreFolder = getOrCreateItem("PRE", tiFolderTemplate, itmExtraNightsFolder);
              itmExtraNightsPostFolder = getOrCreateItem("POST", tiFolderTemplate, itmExtraNightsFolder);
              foreach (XmlNode ndExtraNightPrice in (ndDateRange.SelectSingleNode("Price").ChildNodes))
              {
                Item itmPriceFolder = ndExtraNight.SelectSingleNode("Type").InnerText == "PRE" ? itmExtraNightsPreFolder : itmExtraNightsPostFolder;
                Item itmPrice = getOrCreateItem(ndExtraNightPrice.Name, tiPriceTemplate, itmPriceFolder);
                itmPrice.Editing.BeginEdit();
                foreach (XmlNode ndCurrency in ndExtraNightPrice.ChildNodes)
                {
                  itmPrice[ndCurrency.Name] = ndCurrency.InnerText;
                }
                itmPrice.Editing.EndEdit();
              }
            }            
          }
        }

        
        XmlNodeList nlDepartures = xmlDoc.SelectNodes("/Tour/Departures/Departure");
        if (nlDepartures.Count > 0)
        {
          Item itmDepartures = getOrCreateItem("Departures", tiFolderTemplate, itmCurrentTour);
          foreach (XmlNode ndDeparture in nlDepartures)
          {
            Item itmDeparture = LoadDeparture(itmDepartures, ndDeparture);
          }
        }

        XmlNodeList nlExcursions = xmlDoc.SelectNodes("/Tour/OptionalExcursions/OptionalExcursion");
        if (nlExcursions.Count > 0)
        {
          itmTour.Editing.BeginEdit();
          foreach (XmlNode ndExcursion in nlExcursions)
          {
            Item itmExcursion = dbMaster.GetItem(String.Format("/sitecore/content/TourData/Excursions/*/*[@Excursion Id=\"{0}\"]", ndExcursion.SelectSingleNode("ExcId").InnerText));
            if (itmExcursion == null || !lstExcursions.Contains(ndExcursion.SelectSingleNode("ExcId").InnerText.Trim()))
            {
              itmExcursion = LoadExcursion(itmExcursionsRoot, ndExcursion);
            }
            itmTour["Optional Excursions"] += String.Format("{0}{1}", string.IsNullOrEmpty(itmTour["Optional Excursions"]) ? string.Empty : "|", itmExcursion.ID.ToString());
          }
          itmTour.Editing.EndEdit();
        }

        //fiFile.CopyTo(string.Format("{0}\\{1}", currentDirectory.FullName, fiFile.Name));
      }
      catch (Exception ex)
      {
        logError(ex.ToString());
        processedOk = false;
      }

      return processedOk;
    }

    private static Item LoadDeparture(Item parentItem, XmlNode xmlDeparture)
    {
      Item itmDeparture = getOrCreateItem(xmlDeparture.SelectSingleNode("StartDate").InnerText, tiDepartureTemplate, parentItem);
      itmDeparture.Editing.BeginEdit();
      itmDeparture["Start Date"] = xmlDeparture.SelectSingleNode("StartDate").InnerText.Replace("-", string.Empty) + "T000000";
      itmDeparture["End Date"] = xmlDeparture.SelectSingleNode("EndDate").InnerText.Replace("-", string.Empty) + "T000000";
      itmDeparture["Guaranteed"] = xmlDeparture.SelectSingleNode("Guaranteed").InnerText == "true" ? "1" : "0";
      itmDeparture["Coach or Vessel"] = xmlDeparture.SelectSingleNode("Coach").InnerText.Trim();
      itmDeparture["Status"] = xmlDeparture.SelectSingleNode("Status").InnerText.Trim();
      itmDeparture["sgl"] = xmlDeparture.SelectSingleNode("DepartureRoomTypes/sgl").InnerText == "true" ? "1" : "0";
      itmDeparture["twn"] = xmlDeparture.SelectSingleNode("DepartureRoomTypes/twn").InnerText == "true" ? "1" : "0";
      itmDeparture["tpl"] = xmlDeparture.SelectSingleNode("DepartureRoomTypes/tpl").InnerText == "true" ? "1" : "0";
      itmDeparture.Editing.EndEdit();
      Item itmPrices = getOrCreateItem("Price", tiFolderTemplate, itmDeparture);
      XmlNodeList nlPrices = xmlDeparture.SelectNodes("Price/*");
      foreach (XmlNode ndPrice in nlPrices)
      {
        LoadPrice(itmPrices, ndPrice);
      }

      XmlNodeList nlHotels = xmlDeparture.SelectNodes("HotelList/Hotel");
      if (nlHotels.Count > 0)
      {
        Item itmHotels = getOrCreateItem("Hotels", tiHotelListTemplate, itmDeparture);
        foreach (XmlNode ndHotel in nlHotels)
        {
          LoadHotel(itmHotels, ndHotel);
        }
      }

      XmlNodeList nlOptionalChages = xmlDeparture.SelectNodes("OptionalCharges/OptionalCharge");
      if (nlOptionalChages.Count > 0)
      {
        Item itmOptionalChages = getOrCreateItem("Optional Charges", tiFolderTemplate, itmDeparture);
        foreach (XmlNode ndCharge in nlOptionalChages)
        {
          Item itmCharge = getOrCreateItem(ndCharge.SelectSingleNode("Name").InnerText, tiOptionalChargeTemplate, itmOptionalChages);
          itmCharge.Editing.BeginEdit();
          itmCharge["Type"] = ndCharge.SelectSingleNode("Type").InnerText.Trim();
          foreach (XmlNode ndPrice in ndCharge.SelectSingleNode("Price").ChildNodes)
          {
            itmCharge[ndPrice.Name] = ndPrice.InnerText.Trim();
          }
          itmCharge.Editing.EndEdit();
        }
      }

      return itmDeparture;
    }

    private static Item LoadExcursion(Item parentItem, XmlNode xmlExcursion)
    {
      String strExcursionName = String.Format("{0}-{1}", xmlExcursion.SelectSingleNode("ExcId").InnerText, ItemUtil.ProposeValidItemName(xmlExcursion.SelectSingleNode("Name").InnerText).Trim());
      Item itmLocation = getOrCreateItem(xmlExcursion.SelectSingleNode("Location").InnerText, tiFolderTemplate, itmExcursionsRoot);
      Item itmExcursion = getOrCreateItem(strExcursionName, tiExcursionTemplate, itmLocation);
      itmExcursion.Editing.BeginEdit();
      itmExcursion["Excursion Id"] = xmlExcursion.SelectSingleNode("ExcId").InnerText;
      itmExcursion["Location"] = xmlExcursion.SelectSingleNode("Location").InnerText;
      itmExcursion["Name"] = xmlExcursion.SelectSingleNode("Name").InnerText;
      itmExcursion["Description"] = xmlExcursion.SelectSingleNode("Description").InnerText;
      itmExcursion.Editing.EndEdit();
      lstExcursions.Add(xmlExcursion.SelectSingleNode("ExcId").InnerText);
      return itmExcursion;
    }

    private static void LoadPrice(Item parentItem, XmlNode xmlPrice)
    {
      Item itmPrice = getOrCreateItem(xmlPrice.Name, tiPriceTemplate, parentItem);
      itmPrice.Editing.BeginEdit();
      foreach (XmlNode ndPrice in xmlPrice.ChildNodes)
      {
        itmPrice[ndPrice.Name] = ndPrice.InnerText;
      }
      itmPrice.Editing.EndEdit();
    }

    private static void LoadHotel(Item parentItem, XmlNode xmlHotel)
    {
      Item itmHotel = itmHotelsRoot.Children[xmlHotel.SelectSingleNode("Name").InnerText];
      if (itmHotel == null)
      {
        itmHotel = getOrCreateItem(xmlHotel.SelectSingleNode("Name").InnerText, tiHotelDetailsTemplate, itmHotelsRoot);
        UpdateHotelDetails(itmHotel, xmlHotel);
      }
      else
      {
        if (!lstHotels.Contains<String>(itmHotel.Name))
        {
          UpdateHotelDetails(itmHotel, xmlHotel);
        }
      }

      Item itmHotelList = getOrCreateItem(String.Format("Day {0} - {1}", int.Parse(xmlHotel.SelectSingleNode("DayNo").InnerText).ToString("D2"), xmlHotel.SelectSingleNode("Name").InnerText), tiHotelListItemTemplate, parentItem);
      itmHotelList.Editing.BeginEdit();
      itmHotelList["Day No"] = xmlHotel.SelectSingleNode("DayNo").InnerText.Trim();
      itmHotelList["Hotel"] = xmlHotel.SelectSingleNode("Name").InnerText.Trim();
      itmHotelList.Editing.EndEdit();
    }

    private static void UpdateHotelDetails(Item itmHotel, XmlNode xmlHotel)
    {
      itmHotel.Editing.BeginEdit();
      itmHotel["Name"] = xmlHotel.SelectSingleNode("Name").InnerText.Trim();
      itmHotel["Address"] = xmlHotel.SelectSingleNode("Address").InnerText.Trim();
      itmHotel["Phone"] = xmlHotel.SelectSingleNode("Phone").InnerText.Trim();
      itmHotel["Fax"] = xmlHotel.SelectSingleNode("Fax").InnerText.Trim();
      string strURL = xmlHotel.SelectSingleNode("url").InnerText.Trim();
      if (!strURL.StartsWith("http://") && !string.IsNullOrEmpty(strURL))
      {
        strURL = string.Format("http://{0}", strURL);
      }
      itmHotel["url"] = string.Format("<link linktype=\"external\" url=\"{0}\" anchor=\"\" target=\"_blank\" />", strURL);
      itmHotel.Editing.EndEdit();
      lstHotels.Add(itmHotel.Name);
    }

    private static Item processTour(string season, string brand, string prodCode, Dictionary<string, string> fieldDictionary)
    {
      itmTours = getOrCreateItem("Tours", tiFolderTemplate, itmTourRoot);
      itmSeason = getOrCreateItem(season, tiFolderTemplate, itmTours);
      itmBrand = getOrCreateItem(brand, tiFolderTemplate, itmSeason);
      itmTourFolder = getOrCreateItem(prodCode.Substring(0, 1), tiFolderTemplate, itmBrand);
      itmTour = getOrCreateItem(prodCode, tiTourTemplate, itmTourFolder);
      itmTour.Editing.BeginEdit();
      foreach (string strKey in fieldDictionary.Keys)
      {
        itmTour[strKey] = fieldDictionary[strKey];
      }
      itmTour.Editing.EndEdit();
      //PublishItem(itmTour);
      return itmTour;
    }

    private static void processItineraryItems(Item TourItem, List<ItineraryDay> itineraryDays)
    {
      itmItineraryFolder = getOrCreateItem("Itinerary", tiFolderTemplate, TourItem);
      foreach (ItineraryDay itineraryDay in itineraryDays)
      {
        itmItineraryDay = getOrCreateItem(itineraryDay.dayNumber.ToString("000"), tiItineraryDayTemplate, itmItineraryFolder);
        itmItineraryDay.Editing.BeginEdit();
        itmItineraryDay["Day Number"] = itineraryDay.dayNumber.ToString();
        itmItineraryDay["Description"] = itineraryDay.description;
        itmItineraryDay["Program"] = itineraryDay.program;
        itmItineraryDay.Editing.EndEdit();
      }      
    }

    private static void processExtraNights(Item TourItem, List<ExtraNight> extraNights)
    {
      itmExtraNightsFolder = getOrCreateItem("Extra Nights", tiFolderTemplate, TourItem);
      itmExtraNightsPreFolder = getOrCreateItem("PRE", tiFolderTemplate, itmExtraNightsFolder);
      itmExtraNightsPostFolder = getOrCreateItem("POST", tiFolderTemplate, itmExtraNightsFolder);
      foreach (ExtraNight extraNight in extraNights)
      {
        if (extraNight.type == "PRE")
        {
          itmExtraNight = getOrCreateItem(extraNight.startDate.ToString("yyyy-MM-dd"), tiDateRangeTemplate, itmExtraNightsPreFolder);
        }
        else
        {
          itmExtraNight = getOrCreateItem(extraNight.startDate.ToString("yyyy-MM-dd"), tiDateRangeTemplate, itmExtraNightsPostFolder);
        }
        itmExtraNight.Editing.BeginEdit();
        itmExtraNight["Start Date"] = string.Format("{0}T000000", extraNight.startDate.ToString("yyyyMMdd"));
        itmExtraNight["End Date"] = string.Format("{0}T000000", extraNight.endDate.ToString("yyyyMMdd"));
        itmExtraNight.Editing.EndEdit();
        // process the prices
      }

      


    }
    private static Item getOrCreateItem(String itemName, TemplateItem itemTemplate, Item ParentItem)
    {
      Item itmNewItem = null;
      try
      {
        string strItemName = ItemUtil.ProposeValidItemName(itemName.Trim());
        itmNewItem = ParentItem.Children[strItemName];
        if (itmNewItem == null)
        {
          itmNewItem = ParentItem.Add(strItemName, itemTemplate);
          //PublishItem(itmNewItem);
        }
      }
      catch (Exception ex)
      {
        logError(ex.ToString());
      }
      return itmNewItem;
    }

    private static void writeLogInfo(string strTextToWriteToLog)
    {
      Sitecore.Diagnostics.Log.Info(string.Format("**** {0}", strTextToWriteToLog), typeof(XmlDataImport));
    }

    private static void logError(string strTextToWriteToLog)
    {
      Sitecore.Diagnostics.Log.Error(string.Format("**** {0}", strTextToWriteToLog), typeof(XmlDataImport));
    }

    private static void PublishItem(Item itmToBePublished)
    {
      Publisher pub = new Publisher(pubOpts);
      pub.Options.RootItem = itmToBePublished;
      pub.Options.Deep = false;
      pub.Publish();
    }

    private static void PublishTours(Item itmToBePublished)
    {
      PublishOptions pubOptsTours = new PublishOptions(dbMaster, dbWeb, PublishMode.Smart, Sitecore.Globalization.Language.Predefined.English, System.DateTime.Now);
      pubOptsTours.RootItem = itmToBePublished;
      Publisher pub = new Publisher(pubOptsTours);
      pub.Options.Deep = true;
      pub.Publish();
    }


    public class ItineraryDay
    {
      public int dayNumber { get; set; }
      public string description { get; set; }
      public string program { get; set; }
    }

    public class ExtraNight
    {
      public string type { get; set; }
      public DateTime startDate { get; set; }
      public DateTime endDate { get; set; }
      public List<Price> prices { get; set; }
    }

    public class Price
    {
      public string code { get; set; }
      public Dictionary<string, int> amounts { get; set; }
    }
  }

  public static class DirectoryExtension
  {
    public static void Empty(this DirectoryInfo directory)
    {
      foreach (FileInfo file in directory.GetFiles()) file.Delete();
      foreach (DirectoryInfo subDir in directory.GetDirectories()) subDir.Delete(true);
    }
  }
}