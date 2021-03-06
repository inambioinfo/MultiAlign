#pragma once
#include "clsDataSummaryAttribute.h"
#include "clsParameterFileAttribute.h"

namespace MultiAlignEngine
{
	namespace MassTags
	{
		
		public __value enum MassTagDatabaseType2
		{
			None,
			SQL,
			ACCESS,
			SQLite
		};

		[System::Serializable]
		public __gc class clsMassTagDatabaseOptions
		{
			public:
				System::Byte mbyteConfirmedTags; 
				
				[clsDataSummaryAttribute("Minimum X-Correlation")]
				[clsParameterFileAttribute("mfltMinXCorr","MassTagDBOptions")]
				float mfltMinXCorr; 

				[clsDataSummaryAttribute("Minimum MS-MS Observations")]
				[clsParameterFileAttribute("mintMinObservationCountFilter","MassTagDBOptions")]
				int mintMinObservationCountFilter;
				
				[clsDataSummaryAttribute("Minimum PMT Score")]
				[clsParameterFileAttribute("mdecimalMinPMTScore","MassTagDBOptions")]
				System::Decimal mdecimalMinPMTScore; 
				
				[clsDataSummaryAttribute("NET Value Type")]
				[clsParameterFileAttribute("mbyteNETValType","MassTagDBOptions")]
				System::Byte mbyteNETValType; //-- 0 to use GANET values, 1 to use PNET values
				
				[clsDataSummaryAttribute("Minimum Discriminant")]				
				[clsParameterFileAttribute("mdblMinDiscriminant","MassTagDBOptions")]
				double mdblMinDiscriminant;
				
				[clsDataSummaryAttribute("Prophet Value")]
				[clsParameterFileAttribute("mdblPeptideProphetVal","MassTagDBOptions")]
				double mdblPeptideProphetVal; 
				
				[clsDataSummaryAttribute("Database Name")]
				System::String *mstrDatabase; 
				
				[clsDataSummaryAttribute("Server Name")]
				System::String *mstrServer; 
				
				[clsDataSummaryAttribute("Experiment Filter")]
				[clsParameterFileAttribute("mstrExperimentFilter","MassTagDBOptions")]
				System::String *mstrExperimentFilter; 

				[clsDataSummaryAttribute("Experiment Exlusion Filter")]
				[clsParameterFileAttribute("mstrExperimentExclusionFilter","MassTagDBOptions")]
				System::String *mstrExperimentExclusionFilter; 
								
				[clsDataSummaryAttribute("Database Type")]
				[clsParameterFileAttribute("menm_databaseType","DatabaseType")]
				MassTagDatabaseType2 menm_databaseType;
				
				[clsDataSummaryAttribute("Database File Path")]
				System::String *mstr_databaseFilePath;
				
				System::String *mstrUserID; 
				System::String *mstrPasswd; 

				clsMassTagDatabaseOptions(void);
				~clsMassTagDatabaseOptions(void);
		};
	}
}
