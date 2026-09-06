using System;
using System.Collections.Generic;

namespace LiverAR.Runtime
{
    [Serializable]
    public sealed class AnatomyInformationRecord
    {
        public string Id;
        public string DisplayName;
        public string Category;
        public string Overview;
        public string Location;
        public string BloodSupply;
        public string VenousDrainage;
        public string Function;
        public string Description;
        public string Source;

        public string ToDisplayText()
        {
            return $"Overview\n{Overview}\n\nAnatomical location\n{Location}\n\nBlood supply\n{BloodSupply}\n\nVenous drainage\n{VenousDrainage}\n\nFunction\n{Function}";
        }
    }

    public static class AnatomyInformationCatalog
    {
        public static AnatomyInformationRecord Liver => new AnatomyInformationRecord
        {
            Id = "liver", DisplayName = "Liver", Category = "Liver",
            Overview = "The liver is the largest internal gland and a major organ of metabolism.",
            Location = "The upper-right abdomen, beneath the diaphragm and beside the stomach.",
            BloodSupply = "The hepatic artery and portal vein provide arterial and nutrient-rich inflow.",
            VenousDrainage = "Hepatic veins drain into the inferior vena cava.",
            Function = "Metabolism, bile production, nutrient storage, detoxification, and plasma protein synthesis.",
            Description = "This educational summary describes normal anatomy and is not medical advice.",
            Source = "OpenStax, Anatomy and Physiology 2e, 23.6\nhttps://openstax.org/books/anatomy-and-physiology-2e/pages/23-6-accessory-organs-in-digestion-the-liver-pancreas-and-gallbladder"
        };

        public static AnatomyInformationRecord ForSegment(string name)
        {
            return new AnatomyInformationRecord
            {
                Id = name.ToLowerInvariant().Replace(" ", "-"), DisplayName = name, Category = "Couinaud Segmentation",
                Overview = $"{name} is one of the functional liver segments in the Couinaud system.",
                Location = "Segment boundaries are defined by portal and hepatic venous anatomy; orientation may vary in the 3D model.",
                BloodSupply = "The segment receives portal inflow through its corresponding portal pedicle.",
                VenousDrainage = "Drainage follows nearby hepatic venous territories.",
                Function = "Segmental anatomy helps describe liver structure and supports educational orientation.",
                Description = "Use the 3D model and segmentation controls to compare this segment with neighbouring regions.",
                Source = "Sergi (ed.), Liver Cancer, NCBI Bookshelf, Chapter 4\nhttps://www.ncbi.nlm.nih.gov/books/NBK569802/"
            };
        }

        public static AnatomyInformationRecord ForPart(AnatomyPart part)
        {
            if (part == null) return Liver;
            if (part.Category == AnatomyCategory.LiverSegment) return ForSegment(part.DisplayName);
            if (part.Category == AnatomyCategory.Lesion) return ForTumor(part.DisplayName);
            if (part.Category == AnatomyCategory.Vessel)
                return new AnatomyInformationRecord
                {
                    Id = part.StructureId, DisplayName = part.DisplayName, Category = "Blood Vessel",
                    Overview = "A vessel structure included in the patient model.", Location = "Shown in relation to the liver in the 3D scene.",
                    BloodSupply = "Vessel-specific inflow depends on the displayed structure.", VenousDrainage = "Vessel-specific drainage depends on the displayed structure.",
                    Function = "Supports blood flow through or away from the liver.", Description = "This educational model does not provide diagnosis or treatment advice.",
                    Source = "OpenStax, Anatomy and Physiology 2e, 20.1 and 23.6\nhttps://openstax.org/books/anatomy-and-physiology-2e/pages/20-1-structure-and-function-of-blood-vessels"
                };
            return Liver;
        }

        public static AnatomyInformationRecord ForTumor(string name)
        {
            return new AnatomyInformationRecord
            {
                Id = "tumor", DisplayName = string.IsNullOrWhiteSpace(name) ? "Tumor" : name, Category = "Tumor",
                Overview = "A tumour is an abnormal mass of cells. In this educational model, the tumour is a patient-specific overlay supplied by the imported GLB.",
                Location = "Its displayed location is aligned to the liver anatomy in the same patient export.",
                BloodSupply = "Tumour blood supply can vary and is not represented as a diagnostic finding in this application.",
                VenousDrainage = "Venous relationships depend on the patient-specific anatomy shown in the imported model.",
                Function = "Use the overlay to study the tumour location relative to liver segments and blood vessels."
            };
        }

        public static readonly string[] SegmentNames = { "Segment I", "Segment II", "Segment III", "Segment IVa", "Segment IVb", "Segment V", "Segment VI", "Segment VII", "Segment VIII" };
        public static readonly string[] DiseaseNames = { "Fatty Liver", "Cirrhosis", "Alcohol-Related Liver Disease", "Liver Cancer", "Hepatitis" };

        public static AnatomyInformationRecord ForDisease(string name)
        {
            var record = new AnatomyInformationRecord
            {
                Id = name.ToLowerInvariant().Replace(" ", "-"), DisplayName = name, Category = "Liver Disease",
                Overview = $"{name} is included as an educational topic in this application.",
                Location = "Changes may affect liver tissue and function.", BloodSupply = "Not applicable to this educational topic.",
                VenousDrainage = "Not applicable to this educational topic.", Function = "The effect on liver function varies by condition and individual.",
                Description = "Educational content only. This application does not provide diagnosis or treatment advice.",
                Source = "Sharma and Nagalli, Chronic Liver Disease, NCBI Bookshelf\nhttps://www.ncbi.nlm.nih.gov/books/NBK554597/"
            };

            if (string.Equals(name, "Fatty Liver", StringComparison.OrdinalIgnoreCase))
            {
                record.Overview = "Fatty liver describes excess fat stored in liver cells. It may occur with metabolic conditions or prolonged alcohol exposure.";
                record.Location = "Fat accumulates within liver tissue and may be distributed throughout the organ.";
                record.Function = "Early fatty change may cause few symptoms, but ongoing injury can lead to inflammation and fibrosis.";
                record.Description = "Risk factors can include obesity, diabetes, abnormal blood lipids, and alcohol use. This model is for learning, not diagnosis.";
            }
            else if (string.Equals(name, "Cirrhosis", StringComparison.OrdinalIgnoreCase))
            {
                record.Overview = "Cirrhosis is advanced scarring in which repeated liver injury changes the normal tissue architecture.";
                record.Location = "Fibrous bands and regenerative nodules can alter the structure of the whole liver.";
                record.Function = "Scarring can reduce normal protein production, detoxification, bile handling, and blood flow through the liver.";
                record.Description = "Cirrhosis has many possible causes and requires professional medical assessment; this panel gives educational context only.";
            }
            else if (string.Equals(name, "Alcohol-Related Liver Disease", StringComparison.OrdinalIgnoreCase))
            {
                record.Overview = "Alcohol-related liver disease describes a spectrum of injury associated with prolonged alcohol exposure.";
                record.Location = "Changes may involve liver cells, inflammatory tissue, and later fibrotic scar tissue.";
                record.Function = "Progressive injury can interfere with metabolism, bile excretion, protein synthesis, and circulation.";
                record.Description = "The spectrum may include fatty change, inflammation, and cirrhosis. The application does not provide treatment advice.";
            }
            else if (string.Equals(name, "Liver Cancer", StringComparison.OrdinalIgnoreCase))
            {
                record.Overview = "Liver cancer is an abnormal growth of cells in or involving liver tissue; hepatocellular carcinoma is a primary liver cancer.";
                record.Location = "A tumour may arise in one region of the liver and can change local anatomy as it grows.";
                record.Function = "A tumour or underlying liver disease can affect normal liver architecture and function.";
                record.Description = "The 3D model illustrates anatomy and does not represent a clinical tumour or support diagnosis.";
            }
            else if (string.Equals(name, "Hepatitis", StringComparison.OrdinalIgnoreCase))
            {
                record.Overview = "Hepatitis means inflammation of the liver and can have infectious, immune, toxic, or metabolic causes.";
                record.Location = "Inflammation affects liver tissue and may be temporary or persist over time, depending on its cause.";
                record.Function = "Persistent inflammation may lead to fibrosis and alter normal liver functions.";
                record.Description = "Different forms of hepatitis behave differently. This educational summary is not a diagnosis or treatment guide.";
            }

            return record;
        }

        public static AnatomyInformationRecord ForVessel(string name)
        {
            return new AnatomyInformationRecord
            {
                Id = name.ToLowerInvariant().Replace(" ", "-"), DisplayName = name, Category = "Blood Vessel",
                Overview = "A blood vessel structure shown in relation to the liver.",
                Location = "The exact course is represented by the currently loaded 3D model.",
                BloodSupply = "Vessel-specific inflow depends on the displayed structure.",
                VenousDrainage = "Vessel-specific drainage depends on the displayed structure.",
                Function = "Supports blood flow through or away from the liver.",
                Description = "This educational model does not provide diagnosis or treatment advice.",
                Source = "OpenStax, Anatomy and Physiology 2e, 20.1 and 23.6\nhttps://openstax.org/books/anatomy-and-physiology-2e/pages/23-6-accessory-organs-in-digestion-the-liver-pancreas-and-gallbladder"
            };
        }
    }
}
