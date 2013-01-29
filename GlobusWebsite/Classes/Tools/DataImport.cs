using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Diagnostics;
using System.Xml;
using System.IO;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Templates;

namespace GlobusWebsite.Classes.Tools
{
    public class DataImport
    {
        private static Database dbMaster = Sitecore.Data.Database.GetDatabase("master");
        private XmlDocument xmlData = new XmlDocument();
        private string strDataFile = "";
        private static TemplateItem tiFolder = dbMaster.GetTemplate(new ID("{A87A00B1-E6DB-45AB-8B54-636FEC3B5523}"));
        private static TemplateItem tiTour = dbMaster.GetTemplate(new ID("{0AFA6B3C-024B-4992-B93B-318FA6E09CCB}"));
        private static TemplateItem tiPublish = dbMaster.GetTemplate(new ID("{7EF1516D-F99E-4C9D-8087-8CDAC27C4312}"));
        private static TemplateItem tiItineraryDay = dbMaster.GetTemplate(new ID("{D63D1124-6DE7-4CDA-B3D4-DACD590D8C36}"));
        private static TemplateItem tiDeparture = dbMaster.GetTemplate(new ID("{BB2E4F1C-47C2-4526-BD6C-60C4157A7219}"));
        private static TemplateItem tiCoach = dbMaster.GetTemplate(new ID("{16955DF4-C207-40DE-80D1-A2AC37245D2F}"));
        private static TemplateItem tiPrice = dbMaster.GetTemplate(new ID("{ED2635D3-494E-4527-94CF-FCDF4ED926C9}"));
        private static TemplateItem tiRoomTypes = dbMaster.GetTemplate(new ID("{073CE6EE-004B-4C3A-80CB-5B7639A8E3CA}"));

        private static Item itmDataRoot = dbMaster.GetItem(new ID("{2A5B3193-C42D-414A-830B-08A635B53B16}"));

        private Item itmGroupFolder;
        private Item itmSeasonFolder;
        private Item itmItineraryFolder;
        private Item itmDeparturesFolder;
        private Item itmCoachesFolder;
        private Item itmPricesFolder;

        private Item itmTour;
        private Item itmPublish;
        private Item itmItineraryDay;
        private Item itmDeparture;
        private Item itmCoach;
        private Item itmPrice;
        private Item itmRoomTypes;

        private string strGroup;

        public void Run()
        {
            Log.Info("**** Starting Data Import ****", this);
            try
            {
                int iCntr = 0;

                if (tiTour != null)
                {
                    foreach (string strFileName in Directory.GetFiles("C:\\Sitecore\\Globus\\Website\\XmlData"))
                    {
                        iCntr = 0;
                        strDataFile = strFileName;
                        strGroup = strFileName.Substring(strFileName.LastIndexOf("\\") + 1).Split(new char[] { '.' })[0];
                        itmGroupFolder = getOrCreateItem(strGroup, tiFolder, itmDataRoot);

                        xmlData.Load(strDataFile);
                        XmlNodeList nlTours = xmlData.SelectNodes("//tour");
                        foreach (XmlNode xnTour in nlTours)
                        {
                            if (((xnTour["publish"]["au"].InnerText == "true" ? 1 : 0) + (xnTour["publish"]["nz"].InnerText == "true" ? 1 : 0) + (xnTour["publish"]["gsa"].InnerText == "true" ? 1 : 0) > 0))
                            {
                                itmSeasonFolder = getOrCreateItem(xnTour["season"].InnerText, tiFolder, itmGroupFolder);
                                
                                //if ((int.Parse(xnTour["season"].InnerText) >= DateTime.Now.Year))
                                {
                                    iCntr++;
                                    LogThis(iCntr.ToString() + " " + xnTour["season"].InnerText + " : " + xnTour["code"].InnerText);
                                    itmTour = getOrCreateItem(xnTour["code"].InnerText, tiTour, itmSeasonFolder);
                                    setTourDetails(xnTour);
                                    itmPublish = getOrCreateItem("Publish", tiPublish, itmTour);
                                    setPublishDetails(xnTour["publish"]);

                                    itmItineraryFolder = itmTour.Children["Itinerary"];
                                    if (itmItineraryFolder == null)
                                    {
                                        itmItineraryFolder = itmTour.Add("Itinerary", tiFolder);
                                    }
                                    foreach (XmlNode xnDay in xnTour["itinerary"].ChildNodes)
                                    {
                                        itmItineraryDay = getOrCreateItem(String.Format("Day {0}", int.Parse(xnDay["day_no"].InnerText).ToString("X3")), tiItineraryDay, itmItineraryFolder);
                                        itmItineraryDay.Editing.BeginEdit();
                                        itmItineraryDay["Day No"] = xnDay["day_no"].InnerText;
                                        itmItineraryDay["Description"] = xnDay["description"].InnerText;
                                        itmItineraryDay["Text"] = xnDay["text"].InnerText;
                                        itmItineraryDay.Editing.EndEdit();
                                    }

                                    if (xnTour["departures"].HasChildNodes)
                                    {
                                        itmDeparturesFolder = getOrCreateItem("Departures", tiFolder, itmTour);
                                        foreach (XmlNode xnDeparture in xnTour["departures"].ChildNodes)
                                        {
                                            itmDeparture = getOrCreateItem(xnDeparture["start_date"].InnerText.Replace("-", "_"), tiDeparture, itmDeparturesFolder);
                                            itmDeparture.Editing.BeginEdit();
                                            itmDeparture["Start Date"] = String.Format("{0}T000000", xnDeparture["start_date"].InnerText.Replace("-", ""));
                                            itmDeparture["End Date"] = String.Format("{0}T000000", xnDeparture["end_date"].InnerText.Replace("-", ""));
                                            itmDeparture["Guaranteed"] = xnDeparture["guaranteed"].InnerText;
                                            itmDeparture.Editing.EndEdit();
                                            if (xnDeparture["coaches"] != null && xnDeparture["coaches"].HasChildNodes)
                                            {
                                                itmCoachesFolder = getOrCreateItem("Coaches and Vessles", tiFolder, itmDeparture);
                                                foreach (XmlNode xnCoach in xnDeparture["coaches"].ChildNodes)
                                                {
                                                    itmCoach = getOrCreateItem("CoachOrVessel", tiCoach, itmCoachesFolder);
                                                    itmCoach.Editing.BeginEdit();
                                                    itmCoach["Name"] = xnCoach["name"].InnerText;
                                                    itmCoach["Allotment Status"] = xnCoach["allotment_status"].InnerText;
                                                    itmCoach.Editing.EndEdit();

                                                    if (xnCoach["price"] != null && xnCoach["price"].HasChildNodes)
                                                    {
                                                        itmPricesFolder = getOrCreateItem("price", tiFolder, itmCoach);
                                                        foreach (XmlNode xnPrice in xnCoach["price"].ChildNodes)
                                                        {
                                                            itmPrice = getOrCreateItem(xnPrice.Name, tiPrice, itmPricesFolder);
                                                            itmPrice.Editing.BeginEdit();
                                                            itmPrice["au"] = xnPrice["au"].InnerText;
                                                            itmPrice["nz"] = xnPrice["nz"].InnerText;
                                                            itmPrice["gsa"] = xnPrice["gsa"].InnerText;
                                                            itmPrice.Editing.EndEdit();
                                                        }
                                                    }

                                                    if (xnCoach["departure_room_types"] != null && xnCoach["departure_room_types"].HasChildNodes)
                                                    {
                                                        itmRoomTypes = getOrCreateItem("Room Types", tiRoomTypes, itmCoach);
                                                        itmRoomTypes.Editing.BeginEdit();
                                                        itmRoomTypes["Single"] = xnCoach["departure_room_types"]["sgl"].InnerText == "true" ? "1" : "0";
                                                        itmRoomTypes["Twin"] = xnCoach["departure_room_types"]["twn"].InnerText == "true" ? "1" : "0";
                                                        itmRoomTypes["Tripple"] = xnCoach["departure_room_types"]["tpl"].InnerText == "true" ? "1" : "0";
                                                        itmRoomTypes.Editing.EndEdit();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    LogThis("Template not found");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString(), this);
            }
        }

        private Item getOrCreateItem (String itemName, TemplateItem itemTemplate, Item ParentItem)
        {
            Item itmNewItem = ParentItem.Children[itemName];
            if (itmNewItem == null)
            {
                itmNewItem = ParentItem.Add(itemName, itemTemplate);
            }
            return itmNewItem;
        }

        private Item TourItemGetOrCreate()
        {
            Item itmTour = null;

            return itmTour;
        }

        private void setTourDetails(XmlNode xnTour)
        {
            itmTour.Editing.BeginEdit();
            itmTour["Season"] = xnTour["season"].InnerText;
            itmTour["Code"] = xnTour["code"].InnerText;
            itmTour["Name"] = xnTour["name"].InnerText;
            itmTour["Duration"] = xnTour["duration"].InnerText;
            itmTour["Start City"] = xnTour["start_city"].InnerText;
            itmTour["End City"] = xnTour["end_city"].InnerText;
            itmTour["Preamble"] = xnTour["preamble"].InnerText;
            itmTour["Meta Keywords"] = xnTour["meta_keywords"].InnerText;
            itmTour["Places Visited"] = xnTour["places_visited"].InnerText;
            itmTour["Meals"] = xnTour["meals"].InnerText;
            itmTour["Features"] = xnTour["features"].InnerText;
            itmTour.Editing.EndEdit();
        }

        private void setPublishDetails(XmlNode xnPublish)
        {
            itmPublish.Editing.BeginEdit();
            itmPublish["au"] = xnPublish["au"].InnerText == "false" ? "0" : "1";
            itmPublish["nz"] = xnPublish["nz"].InnerText == "false" ? "0" : "1";
            itmPublish["gsa"] = xnPublish["gsa"].InnerText == "false" ? "0" : "1";
            itmPublish.Editing.EndEdit();
        }

        private void LogThis(string message)
        {
            Log.Info("**** " + message, this);
        }
    }
}