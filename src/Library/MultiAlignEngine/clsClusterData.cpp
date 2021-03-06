
#include ".\clsclusterData.h"
#include "clsMassTimeTag.h"
#using <mscorlib.dll>

using namespace System::Collections::Generic;

namespace MultiAlignEngine
{
	namespace Features
	{
		clsClusterData::clsClusterData(MultiAlignEngine::Features::clsUMC *arrUMCs __gc[])
		{
			menmClusterRepresentativeType = MultiAlignEngine::Clustering::enmClusterRepresentativeType::MEAN; 
			int numUMCs = arrUMCs->Length; 
			if (numUMCs == 0)
				return; 

			mintNumDatasets = arrUMCs[numUMCs-1]->mint_dataset_index + 1;
			marrClusterUMCIndexPair = new System::Collections::ArrayList(); 

			for (int umcNum = 0; umcNum < numUMCs; umcNum++)
			{
				clsPair *pair = new clsPair(arrUMCs[umcNum]->mint_cluster_index, umcNum); 
				if (pair->mintClusterNum > mintNumClusters-1)
					mintNumClusters = pair->mintClusterNum+1; 
				marrClusterUMCIndexPair->Add(pair); 
			}
			marrClusterUMCIndexPair->Sort(); 
			CreateClustersFromClusterUMCIndexPair(arrUMCs); 
		}
		clsClusterData::clsClusterData(MultiAlignEngine::Features::clsUMC *arrUMCs __gc[], MultiAlignEngine::Clustering::enmClusterRepresentativeType centroidType)
		{
			menmClusterRepresentativeType = centroidType;
			int numUMCs = arrUMCs->Length; 
			if (numUMCs == 0)
				return; 

			mintNumDatasets = arrUMCs[numUMCs-1]->mint_dataset_index + 1;
			marrClusterUMCIndexPair = new System::Collections::ArrayList(); 

			Dictionary<int, int>* datasetMap = new Dictionary<int, int>();

			mintNumDatasets = 0;
			for (int umcNum = 0; umcNum < numUMCs; umcNum++)
			{
				int datasetID = arrUMCs[umcNum]->mint_dataset_id;
				if (!datasetMap->ContainsKey(datasetID))
				{
					mintNumDatasets++;
					datasetMap->Add(datasetID, 0);
				}

				clsPair *pair = new clsPair(arrUMCs[umcNum]->mint_cluster_index, umcNum); 
				if (pair->mintClusterNum > mintNumClusters-1)
					mintNumClusters = pair->mintClusterNum+1; 
				marrClusterUMCIndexPair->Add(pair); 
			}
			marrClusterUMCIndexPair->Sort(); 
			CreateClustersFromClusterUMCIndexPair(arrUMCs); 
		}

		clsClusterData::clsClusterData()
		{
			menmClusterRepresentativeType	= MultiAlignEngine::Clustering::enmClusterRepresentativeType::MEDIAN;
			menmClusterIntensityType		= MultiAlignEngine::Clustering::enmClusterIntensityType::SUM_PER_DATASET;
			marrClusters					= new System::Collections::ArrayList(); 
		}

		clsClusterData::~clsClusterData(void)
		{
		}

		void clsClusterData::SetClusterCalibratedMassedAndNETS( std::vector <int> &clusterIndices,
																std::vector<double> &clusterCalibratedMasses, 
																std::vector<double> &clusterAlignedNETS,
																std::vector<double> &clusterDriftTimes)
		{
			int numClusters = (int) clusterIndices.size(); 
			for (int clusterNum = 0; clusterNum < numClusters; clusterNum++)
			{
				int clusterIndex					= clusterIndices[clusterNum]; 
				double calibratedMass				= clusterCalibratedMasses[clusterNum]; 
				double alignedNET					= clusterAlignedNETS[clusterNum]; 
				double driftTime 					= clusterDriftTimes[clusterNum];

				clsCluster *clustA					= dynamic_cast<clsCluster*>(marrClusters->Item[clusterIndex]); 
				clustA->mdouble_mass_calibrated		= calibratedMass;
				clustA->mdouble_aligned_net			= alignedNET;
				clustA->mdouble_driftTime			= driftTime;

				clsCluster *clustB = dynamic_cast<clsCluster*>(marrClusters->Item[clusterIndex]); 
			}
		}

		void clsClusterData::CreateClustersFromClusterUMCIndexPair(MultiAlignEngine::Features::clsUMC *arrUMCs __gc[])
		{
			marrClusters = new System::Collections::ArrayList(mintNumClusters); 
			marrClusterIntensity = new double __gc [mintNumClusters*mintNumDatasets]; 
			marrClusterMainMemberIndex = new int __gc[mintNumClusters*mintNumDatasets]; 

			for (int index = 0; index < mintNumClusters*mintNumDatasets; index++)
			{
				marrClusterIntensity[index] = System::Double::NaN; 
				marrClusterMainMemberIndex[index] = -1; 
			}


			// Go through each cluster. 
			int numElements = marrClusterUMCIndexPair->Count; 
			if (numElements == 0)
				return;

			System::Collections::ArrayList *masses			 = new System::Collections::ArrayList(); 
			System::Collections::ArrayList *calibratedMasses = new System::Collections::ArrayList(); 
			System::Collections::ArrayList *scans			 = new System::Collections::ArrayList(); 
			System::Collections::ArrayList *nets			 = new System::Collections::ArrayList(); 
			System::Collections::ArrayList *drifts			 = new System::Collections::ArrayList(); 
			System::Collections::ArrayList *charges			 = new System::Collections::ArrayList(); 

			clsUMC	*umc; 
			clsPair *pair			= dynamic_cast<clsPair*>(marrClusterUMCIndexPair->Item[0]); 
			int		lastClusterNum	= pair->mintClusterNum;
			int		num_nonzero; 
			double	mass,	calibratedMass, driftTime, avg_driftTime; 
			short	charge, avg_charge; 
			int		scan,	mid_index; 
			int		avg_scan;
			int		pt_num; 
			double	avgMass, avgCalibratedMass; 
			double	net,	 avg_net;  

			mdblMinNET = System::Double::MaxValue; 
			mdblMaxNET = System::Double::MinValue; 
 
			for (int elementNum = 0; elementNum < numElements; elementNum++)
			{
				pair = dynamic_cast<clsPair*>(marrClusterUMCIndexPair->Item[elementNum]); 
				if (pair->mintClusterNum != lastClusterNum)
				{
					num_nonzero						  = masses->Count; 
					clsCluster *clust				  = new clsCluster(); 
					clust->Id = elementNum;
					
					int clusterStartIndex			  = lastClusterNum * mintNumDatasets;
					int clusterStopIndex			  = (lastClusterNum +1)* mintNumDatasets;
					clust->mshort_num_dataset_members = 0;
					for (int currentPtIndex = clusterStartIndex; currentPtIndex < clusterStopIndex; currentPtIndex++)
					{
						if (marrClusterMainMemberIndex[currentPtIndex] != -1)
							clust->mshort_num_dataset_members++; 
					}

					switch (menmClusterRepresentativeType)
					{
						case MultiAlignEngine::Clustering::enmClusterRepresentativeType::MEDIAN:
							if (num_nonzero == 1)
							{
								mass			= *dynamic_cast<double __gc*>(masses->Item[0]); 
								calibratedMass	= *dynamic_cast<double __gc*>(calibratedMasses->Item[0]); 
								scan			= *dynamic_cast<int __gc*>(scans->Item[0]); 
								net				= *dynamic_cast<double __gc*>(nets->Item[0]); 
								charge			= *dynamic_cast<short __gc*>(charges->Item[0]); 
								driftTime		= *dynamic_cast<double __gc*>(drifts->Item[0]); 
							}
							else
							{
								masses->Sort(); 
								calibratedMasses->Sort(); 
								scans->Sort(); 
								nets->Sort(); 
								charges->Sort(); 
								drifts->Sort();

								if (num_nonzero%2 == 0)
								{
									mid_index	    = num_nonzero/2 - 1; 
									mass			= (*dynamic_cast<double __gc*>(masses->Item[mid_index]) + *dynamic_cast<double __gc*>(masses->Item[mid_index+1]))/2; 
									calibratedMass  = (*dynamic_cast<double __gc*>(calibratedMasses->Item[mid_index]) + *dynamic_cast<double __gc*>(calibratedMasses->Item[mid_index+1]))/2; 
									scan			= (*dynamic_cast<int __gc*>(scans->Item[mid_index]) + *dynamic_cast<int __gc*>(scans->Item[mid_index+1]))/2; 
									net				= (*dynamic_cast<double __gc*>(nets->Item[mid_index]) + *dynamic_cast<double __gc*>(nets->Item[mid_index+1]))/2; 
									charge			= *dynamic_cast<short __gc*>(charges->Item[mid_index]); 
									driftTime		= (*dynamic_cast<double __gc*>(drifts->Item[mid_index])+ *dynamic_cast<double __gc*>(drifts->Item[mid_index+1]))/2; 
								}
								else
								{
									mid_index		= num_nonzero/2; 
									mass			= *dynamic_cast<double __gc*>(masses->Item[mid_index]); 
									calibratedMass	= *dynamic_cast<double __gc*>(calibratedMasses->Item[mid_index]);
									scan			= *dynamic_cast<int __gc*>(scans->Item[mid_index]); 
									net				= *dynamic_cast<double __gc*>(nets->Item[mid_index]);
									charge			= *dynamic_cast<short __gc*>( charges->Item[mid_index]);
									driftTime		= *dynamic_cast<double __gc*>( drifts->Item[mid_index]);
								}
							}
							clust->mdouble_mass				= calibratedMass; 
							clust->mdouble_mass_calibrated	= calibratedMass; 
							clust->mint_scan				= scan; 
							clust->mdouble_net				= net; 
							clust->mshort_charge			= charge; 
							clust->mdouble_driftTime		= driftTime;
							//marrClusters->Add(clust); 
							if (net < mdblMinNET)
								mdblMinNET = net; 
							if (net > mdblMaxNET)
								mdblMaxNET = net; 
							break; 
						case MultiAlignEngine::Clustering::enmClusterRepresentativeType::MEAN:
							avgMass				= 0; 
							avgCalibratedMass	= 0; 
							avg_scan			= 0; 
							avg_net				= 0; 
							avg_charge			= 0; 
							avg_driftTime		= 0;
							for (pt_num = 0; pt_num < num_nonzero; pt_num++)
							{
								avgMass				+= *dynamic_cast<double __gc*>(masses->Item[pt_num]); 
								avgCalibratedMass	+= *dynamic_cast<double __gc*>(calibratedMasses->Item[pt_num]); 
								avg_scan			+= *dynamic_cast<int __gc*>(scans->Item[pt_num]); 
								avg_net				+= *dynamic_cast<double __gc*>(nets->Item[pt_num]); 
								avg_charge			+= *dynamic_cast<short __gc*>( charges->Item[pt_num]); 
								avg_driftTime		+= *dynamic_cast<double __gc*>( drifts->Item[pt_num]); 
							}
							avgMass				/= num_nonzero;
							avgCalibratedMass	/= num_nonzero;
							avg_scan			/= num_nonzero; 
							avg_net				/= num_nonzero; 
							avg_charge			/= num_nonzero; 
							avg_driftTime		/= num_nonzero;

							clust->mdouble_mass				= avgCalibratedMass; 
							clust->mdouble_mass_calibrated	= avgCalibratedMass; 
							clust->mint_scan				= avg_scan; 
							clust->mdouble_net				= avg_net; 
							clust->mshort_charge			= avg_charge; 
							clust->mdouble_driftTime		= avg_driftTime;
							//marrClusters->Add(clust); 
							
							if (avg_net < mdblMinNET)
								mdblMinNET =avg_net; 
							if (avg_net > mdblMaxNET)
								mdblMaxNET = avg_net; 
							break; 
					}

					// Given the centroid above calculate the average via Euclidean Distance 
					double sumDistance = 0;
					double distance    = 0;

					List<double>* distances = new List<double>();
					for (pt_num = 0; pt_num < num_nonzero; pt_num++)
					{
						double calibratedMass   = *dynamic_cast<double __gc*>(calibratedMasses->Item[pt_num]); 						
						double net				= *dynamic_cast<double __gc*>(nets->Item[pt_num]); 
						double drift			= System::Convert::ToDouble(*dynamic_cast<double __gc*>(drifts->Item[pt_num])); 

						distance =	Math::Pow(clust->mdouble_mass_calibrated - calibratedMass, 2.0) + 
									Math::Pow(clust->mdouble_aligned_net	 - net, 2.0) + 
									Math::Pow(clust->mdouble_driftTime	  	 - drift, 2.0);
						distance =  Math::Sqrt(distance);
						sumDistance += distance;
						distances->Add(distance);
					}
					distances->Sort();

					clust->MeanScore= sumDistance / num_nonzero;
					int count    = distances->Count;
					int midIndex = 0;
					if ((count % 2) == 0)
					{
						// if they are even
						midIndex		  = count / 2;
						int lowerMidIndex = midIndex - 1;						
						clust->MedianScore = (distances->Item[midIndex] + distances->Item[lowerMidIndex]) / 2.0;
					}
					else
					{
						midIndex     = (count - 1) / 2; 
						clust->MedianScore = distances->Item[midIndex];
					}			

					marrClusters->Add(clust); 

					lastClusterNum = pair->mintClusterNum;
					masses->Clear(); 
					scans->Clear();
					nets->Clear(); 
					charges->Clear(); 
					drifts->Clear();
					calibratedMasses->Clear(); 
				}
				int index = pair->mintUMCIndex; 
				umc = arrUMCs[index]; 

				double rep_mass_calibrated = umc->mdouble_mono_mass_calibrated; 
				double rep_mass = umc->mdouble_mono_mass; 
				int rep_scan = umc->mint_scan_aligned; 
				double rep_net = umc->mdouble_net; 
				double intensity = umc->mdouble_abundance; 

				int pt_index = lastClusterNum * mintNumDatasets + umc->mint_dataset_index; 

				if(System::Double::IsNaN(marrClusterIntensity[pt_index]) || marrClusterIntensity[pt_index] < intensity)
				{
					marrClusterMainMemberIndex[pt_index] = index;
				}

				if (menmClusterIntensityType == MultiAlignEngine::Clustering::enmClusterIntensityType::SUM_PER_DATASET && !System::Double::IsNaN(marrClusterIntensity[pt_index]))
				{
					marrClusterIntensity[pt_index] += intensity;
				}
				else
				{
					if(System::Double::IsNaN(marrClusterIntensity[pt_index]) || marrClusterIntensity[pt_index] < intensity)
					{
						marrClusterIntensity[pt_index] = intensity;
					}
				}


				masses->Add(__box(rep_mass)); 
				calibratedMasses->Add(__box(rep_mass_calibrated)); 
				scans->Add(__box(rep_scan)); 
				nets->Add(__box(rep_net)); 
				charges->Add(__box(umc->mshort_class_rep_charge)); 
				drifts->Add(__box(umc->DriftTime)); 
			}

			num_nonzero = masses->Count; 
			clsCluster *clust = new clsCluster(); 
			int clusterStartIndex = lastClusterNum * mintNumDatasets;
			int clusterStopIndex = (lastClusterNum +1)* mintNumDatasets;
			clust->mshort_num_dataset_members = 0;
			clust->Id = lastClusterNum;
			for (int currentPtIndex = clusterStartIndex; currentPtIndex < clusterStopIndex; currentPtIndex++)
			{
				if (marrClusterMainMemberIndex[currentPtIndex] != -1)
					clust->mshort_num_dataset_members++; 
			}

			switch (menmClusterRepresentativeType)
			{
				case MultiAlignEngine::Clustering::enmClusterRepresentativeType::MEDIAN:
					if (num_nonzero == 1)
					{
						mass = *dynamic_cast<double __gc*>(masses->Item[0]); 
						calibratedMass = *dynamic_cast<double __gc*>(calibratedMasses->Item[0]); 
						scan = *dynamic_cast<int __gc*>(scans->Item[0]); 
						net = *dynamic_cast<double __gc*>(nets->Item[0]); 
						charge = *dynamic_cast<short __gc*>(charges->Item[0]); 
						driftTime		= *dynamic_cast<double __gc*>(drifts->Item[0]); 
					}
					else
					{
						masses->Sort(); 
						calibratedMasses->Sort(); 
						scans->Sort(); 
						nets->Sort(); 
						charges->Sort(); 
						drifts->Sort();

						if (num_nonzero%2 == 0)
						{
							mid_index = num_nonzero/2 - 1; 
							mass = (*dynamic_cast<double __gc*>(masses->Item[mid_index]) + *dynamic_cast<double __gc*>(masses->Item[mid_index+1]))/2; 
							calibratedMass = (*dynamic_cast<double __gc*>(calibratedMasses->Item[mid_index]) + *dynamic_cast<double __gc*>(calibratedMasses->Item[mid_index+1]))/2; 
							scan = (*dynamic_cast<int __gc*>(scans->Item[mid_index]) + *dynamic_cast<int __gc*>(scans->Item[mid_index+1]))/2; 
							net = (*dynamic_cast<double __gc*>(nets->Item[mid_index]) + *dynamic_cast<double __gc*>(nets->Item[mid_index+1]))/2; 
							charge = *dynamic_cast<short __gc*>(charges->Item[mid_index]); 
									driftTime		= (*dynamic_cast<double __gc*>(drifts->Item[mid_index])+ *dynamic_cast<double __gc*>(drifts->Item[mid_index+1]))/2; 
						}
						else
						{
							mid_index = num_nonzero/2; 
							mass = *dynamic_cast<double __gc*>(masses->Item[mid_index]); 
							calibratedMass = *dynamic_cast<double __gc*>(calibratedMasses->Item[mid_index]); 
							scan = *dynamic_cast<int __gc*>(scans->Item[mid_index]); 
							net = *dynamic_cast<double __gc*>(nets->Item[mid_index]);
							charge = *dynamic_cast<short __gc*>( charges->Item[mid_index]);
									driftTime		= *dynamic_cast<double __gc*>( drifts->Item[mid_index]);
						}
					}
					clust->mdouble_mass = mass; 
					clust->mdouble_mass_calibrated = calibratedMass; 
					clust->mint_scan = scan; 
					clust->mdouble_net = net; 
					clust->mshort_charge = charge; 
							clust->mdouble_driftTime		= driftTime;
					marrClusters->Add(clust); 
					if (net < mdblMinNET)
						mdblMinNET = net; 
					if (net > mdblMaxNET)
						mdblMaxNET = net; 
					break; 
				case MultiAlignEngine::Clustering::enmClusterRepresentativeType::MEAN:
					avgMass = 0; 
					avgCalibratedMass = 0; 
					avg_scan = 0; 
					avg_net = 0; 
					avg_charge = 0; 
							avg_driftTime		= 0;
					for (pt_num = 0; pt_num < num_nonzero; pt_num++)
					{
						avgMass += *dynamic_cast<double __gc*>(masses->Item[pt_num]); 
						avgCalibratedMass += *dynamic_cast<double __gc*>(masses->Item[pt_num]); 
						avg_scan += *dynamic_cast<int __gc*>(scans->Item[pt_num]); 
						avg_net += *dynamic_cast<double __gc*>(nets->Item[pt_num]); 
						avg_charge += *dynamic_cast<short __gc*>( charges->Item[pt_num]); 
								avg_driftTime		+= *dynamic_cast<double __gc*>( drifts->Item[pt_num]); 
					}
					avgMass /= num_nonzero;
					avgCalibratedMass /= num_nonzero;
					avg_scan /= num_nonzero; 
					avg_net /= num_nonzero; 
					avg_charge /= num_nonzero; 
							avg_driftTime		/= num_nonzero;
					clust->mdouble_mass = avgMass; 
					clust->mdouble_mass_calibrated = avgCalibratedMass; 
					clust->mint_scan = avg_scan; 
					clust->mdouble_net = avg_net; 
					clust->mshort_charge = avg_charge; 
							clust->mdouble_driftTime		= avg_driftTime;
					marrClusters->Add(clust); 
					if (avg_net < mdblMinNET)
						mdblMinNET = avg_net; 
					if (avg_net > mdblMaxNET)
						mdblMaxNET = avg_net; 
					break; 
			}
// Given the centroid above calculate the average via Euclidean Distance 
					double sumDistance = 0;
					double distance    = 0;

					List<double>* distances = new List<double>();
					for (pt_num = 0; pt_num < num_nonzero; pt_num++)
					{
						double calibratedMass   = *dynamic_cast<double __gc*>(calibratedMasses->Item[pt_num]); 						
						double net				= *dynamic_cast<double __gc*>(nets->Item[pt_num]); 
						double drift			= System::Convert::ToDouble(*dynamic_cast<double __gc*>(drifts->Item[pt_num])); 

						distance =	Math::Pow(clust->mdouble_mass_calibrated - calibratedMass, 2.0) + 
									Math::Pow(clust->mdouble_aligned_net	 - net, 2.0) + 
									Math::Pow(clust->mdouble_driftTime	  	 - drift, 2.0);
						distance =  Math::Sqrt(distance);
						sumDistance += distance;
						distances->Add(distance);
					}
					distances->Sort();

					clust->MeanScore= sumDistance / num_nonzero;
					int count    = distances->Count;
					int midIndex = 0;
					if ((count % 2) == 0)
					{
						// if they are even
						midIndex		  = count / 2;
						int lowerMidIndex = midIndex - 1;						
						clust->MedianScore = (distances->Item[midIndex] + distances->Item[lowerMidIndex]) / 2.0;
					}
					else
					{
						midIndex     = (count - 1) / 2; 
						clust->MedianScore = distances->Item[midIndex];
					}			


			lastClusterNum = pair->mintClusterNum;
			masses->Clear(); 
			calibratedMasses->Clear(); 
			scans->Clear();
			nets->Clear(); 
			charges->Clear(); 
		}
		void clsClusterData::AddCluster(clsCluster *cluster)
		{
			marrClusters->Add(cluster);
			mintNumClusters = marrClusters->Count;
			
		}
 		System::Collections::ArrayList* clsClusterData::GetMassAndTimeTags()
		{
			System::Collections::ArrayList *arrMSFeatures = new System::Collections::ArrayList(mintNumClusters);
			for (int clusterNum = 0; clusterNum < mintNumClusters; clusterNum++)
			{
				clsCluster *cluster = dynamic_cast<Features::clsCluster *>(marrClusters->Item[clusterNum]); 
									
				clsMassTimeTag *mtTag = new clsMassTimeTag(	cluster->mdouble_mass_calibrated, 
															cluster->mdouble_aligned_net, 
															cluster->mdouble_driftTime,
															clusterNum);
				arrMSFeatures->Add(mtTag); 
			}
			return arrMSFeatures; 
		}
 		System::Collections::ArrayList* clsClusterData::GetMassAndTimeTags(double shiftDaltons)
		{
			System::Collections::ArrayList *arrMSFeatures = new System::Collections::ArrayList(mintNumClusters);
			for (int clusterNum = 0; clusterNum < mintNumClusters; clusterNum++)
			{
				clsCluster *cluster = dynamic_cast<Features::clsCluster *>(marrClusters->Item[clusterNum]); 
									
				clsMassTimeTag *mtTag = new clsMassTimeTag(	cluster->mdouble_mass_calibrated + shiftDaltons, 
															cluster->mdouble_aligned_net,
															cluster->mdouble_driftTime,
															clusterNum);
				arrMSFeatures->Add(mtTag); 
			}
			return arrMSFeatures; 
		}

	}
}