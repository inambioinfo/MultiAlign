#region

using System;
using System.Collections;
using System.Collections.Specialized;

#endregion

namespace MultiAlignCore.Data.Factors
{
    /// <summary>
    ///     Manages a group of tree nodes and sorting methods.
    /// </summary>
    public class FactorTree : IComparer, ICloneable
    {
        private ArrayList m_factorList;

        /// <summary>
        ///     Holds the data names as keys, and the factor values (text) for hashing.
        /// </summary>
        private ArrayList m_datasetList;

        /// <summary>
        ///     Current value of the tree built from hashtables and factors supplied.
        /// </summary>
        private TreeNode m_currentTree;

        /// <summary>
        ///     Flag if the tree-hash has been modified and needs to be re-sorted.
        /// </summary>
        private bool m_treeDirty;

        /// <summary>
        ///     Mapping when data sets are sorted and re-arranged to synchronize data.
        /// </summary>
        private long[] m_datasetMapping;

        /// <summary>
        ///     Constructor for the tree.
        /// </summary>
        public FactorTree()
        {
            m_datasetList = new ArrayList();
            m_factorList = new ArrayList();
            m_currentTree = null;
            m_treeDirty = true;
        }

        /// <summary>
        ///     Data array list that holds the data hash tables.
        /// </summary>
        public ArrayList Data
        {
            get { return m_datasetList; }
            set { m_datasetList = value; }
        }

        public long[] DatasetMapping
        {
            get { return m_datasetMapping; }
            set { m_datasetMapping = value; }
        }

        public ArrayList Factors
        {
            get { return m_factorList; }
            set { m_factorList = value; }
        }

        /// <summary>
        ///     Adds a factor to the tree.
        /// </summary>
        /// <param name="factorName"></param>
        /// <param name="table"></param>
        public void AddFactor(string factorName, StringCollection table)
        {
            m_treeDirty = true;
            var newFactor = new FactorData(factorName, table);
            m_factorList.Add(newFactor);
        }

        /// <summary>
        ///     Clears the tree-hash structure
        /// </summary>
        public void ClearFactors()
        {
            m_factorList.Clear();
        }

        /// <summary>
        ///     Adds a data set and its factor/factor assignments to the tree-hash structure.
        /// </summary>
        /// <param name="dataName">Dataset name</param>
        /// <param name="factors">Hashtable of factors.  Factor name is key, factor value is value.</param>
        public void AddData(string dataName, Hashtable factors)
        {
            var newData = new FactorDataset(dataName, factors);
            long index = m_datasetList.Add(newData);
            newData.Index = index;
            m_treeDirty = true;
        }

        /// <summary>
        ///     Builds a tree from the hash-table for visualization or hierachy parsing.
        /// </summary>
        /// <returns></returns>
        public TreeNode BuildTree()
        {
            m_currentTree = null;
            m_currentTree = new TreeNode();

            // Only sort if there were changes made to the ordering.
            if (m_treeDirty)
            {
                ///
                /// This object implements the IComparer interface for sorting.
                ///
                var tempDatasort = new ArrayList();
                tempDatasort = m_datasetList.Clone() as ArrayList;
                if (m_datasetMapping == null || m_datasetMapping.Length != m_datasetList.Count)
                {
                    m_datasetMapping = new long[m_datasetList.Count];
                }

                m_datasetList.Sort(this);

                /// Un-mapped items will be -1
                for (var i = 0; i < tempDatasort.Count; i++)
                {
                    var oldName = tempDatasort[i] as FactorDataset;
                    for (var j = 0; j < m_datasetList.Count; j++)
                    {
                        var newName = m_datasetList[j] as FactorDataset;
                        if (oldName.Name == newName.Name)
                        {
                            m_datasetMapping[oldName.Index] = j;
                            break;
                        }
                        m_datasetMapping[oldName.Index] = -1;
                    }
                }

                // Tree has been sorted.  Next time we can skip this step.
                m_treeDirty = false;
            }

            if (m_datasetList.Count <= 0)
                return m_currentTree;

            ///
            /// Now build the tree
            ///
            var root = m_currentTree;

            foreach (FactorDataset data in  m_datasetList)
            {
                var tempRootNode = root;
                var createNew = false;
                long i = 0;
                ///
                /// Foreach factor, find where the tree needs to branch.
                ///
                foreach (FactorData factor in m_factorList)
                {
                    ///
                    /// Get the factor value information
                    ///
                    var factorValue = data.Values[factor.Name] as string;
                    // If we have children compare to the last item.
                    if (factorValue == null)
                    {
                        factorValue = "<undefined>";
                        createNew = true;
                    }
                    else if (createNew == false && tempRootNode.Children != null && tempRootNode.Children.Count > 0)
                    {
                        ///
                        /// The last node should be the one we need to examine against.
                        ///
                        var node = tempRootNode.Children[tempRootNode.Children.Count - 1] as TreeNode;
                        if (node.Name != factorValue)
                            createNew = true;
                        else
                            tempRootNode = node;
                    }
                    else
                    {
                        createNew = true;
                    }
                    if (createNew)
                    {
                        var newNode = new TreeNode();
                        newNode.Name = factorValue;
                        newNode.Parent = tempRootNode;
                        newNode.Level = i;
                        tempRootNode.Children.Add(newNode);
                        tempRootNode = newNode;
                    }
                    i++;
                }

                ///
                /// Add the dataset
                ///
                var dataNode = new TreeNode();
                dataNode.Name = data.Name;
                dataNode.Parent = tempRootNode;
                dataNode.Level = i;
                tempRootNode.Children.Add(dataNode);
            }
            return m_currentTree;
        }

        #region IComparer Members

        /// <summary>
        ///     Compares two objects of clsFactorDataset type.
        /// </summary>
        /// <param name="x">dataset one.</param>
        /// <param name="y">dataset two</param>
        /// <returns>0 for equal, 1 for x > y, -1 for y > x</returns>
        public int Compare(object x, object y)
        {
            var data1 = x as FactorDataset;
            var data2 = y as FactorDataset;

            ///
            /// We have to look at all the factor information
            ///
            foreach (FactorData factor in m_factorList)
            {
                var factorValue1 = data1.Values[factor.Name] as string;
                var factorValue2 = data2.Values[factor.Name] as string;

                ///
                /// If they are both the same, continue on, even if they are null
                /// This compares the two strings, if they are equal, dont bother
                /// hashing the factor table for their respective values
                /// If they are both null....great...continue on.
                ///
                if (factorValue1 == factorValue2)
                    continue;

                /// If one is null then we know the other is greater than
                if (factorValue1 == null)
                    return -1;
                if (factorValue2 == null)
                    return 1;

                ///
                /// Otherwise we need to compare the two orders of the factor values.
                /// This will give us respective ordering
                var obj1 = factor.Values[factorValue1];
                var obj2 = factor.Values[factorValue2];
                var index1 = (long) obj1;
                var index2 = (long) obj2;

                if (index1 > index2) return 1;
                return -1;
            }

            if (data1.Index > data2.Index)
                return 1;
            if (data1.Index == data2.Index)
                return 0;

            return -1;
        }

        #endregion

        #region ICloneable Members

        public object Clone()
        {
            var newTree = new FactorTree();
            foreach (FactorData o in Factors)
            {
                newTree.Factors.Add(o);
            }

            foreach (FactorDataset data in  Data)
            {
                newTree.Data.Add(data);
            }

            if (DatasetMapping != null)
            {
                newTree.DatasetMapping = new long[DatasetMapping.Length];
                DatasetMapping.CopyTo(newTree.DatasetMapping, 0);
            }

            return newTree;
        }

        #endregion
    }


    /// <summary>
    ///     Describes a factor or group entity.  Holds a collection of factor values with their sorted keys.
    /// </summary>
    public class FactorData : ICloneable
    {
        /// <summary>
        ///     Name of the factor.
        /// </summary>
        private string m_name;

        /// <summary>
        ///     Index in the list.
        /// </summary>
        private long m_index;

        /// <summary>
        ///     Hashtable of factor values
        /// </summary>
        private Hashtable m_factorValues;

        /// <summary>
        ///     Constructor for the factor.  Name is the key of the factor.
        /// </summary>
        /// <param name="name">Key for use in the collection</param>
        public FactorData(string name)
        {
            m_name = name;
            m_factorValues = null;
        }

        /// <summary>
        ///     Constructor for the factor.  Creates a factor and underlying hash-table from pre-sorted array list of factor
        ///     values.
        /// </summary>
        /// <param name="name">Key for use in the collection.</param>
        /// <param name="factorValues">Collection of sorted-factor values.</param>
        public FactorData(string name, StringCollection factorValues)
        {
            m_name = name;
            m_factorValues = new Hashtable();

            ///
            /// For every string in the string collection
            /// add it to the hash table, store the order it was added because the
            /// string collection should already be sorted for us.  This way
            /// later we can hash out the order of the value for a comparison in a sorting
            /// algorithm.
            ///
            long i = 0;
            foreach (var factorValueString in factorValues)
            {
                m_factorValues[factorValueString] = i++;
            }
        }

        /// <summary>
        ///     Name of the factor.
        /// </summary>
        public string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        /// <summary>
        ///     Key-index of the factor in a collection.
        /// </summary>
        public long Index
        {
            get { return m_index; }
            set { m_index = value; }
        }

        /// <summary>
        ///     Hashtable of factor values.
        /// </summary>
        public Hashtable Values
        {
            get { return m_factorValues; }
            set { m_factorValues = value; }
        }

        #region ICloneable Members

        public object Clone()
        {
            var newFactor = new FactorData(Name);
            foreach (string key in m_factorValues.Keys)
            {
                newFactor.Values.Add(key, m_factorValues[key]);
            }

            newFactor.Index = Index;
            newFactor.Name = Name;
            return newFactor;
        }

        #endregion
    }

    /// <summary>
    ///     Describes a dataset with some factor information.
    /// </summary>
    public class FactorDataset : ICloneable
    {
        private string m_datasetName;
        private Hashtable m_factorKeys;
        private long m_index;

        /// <summary>
        ///     constructor for a dataset.  Holds the name of the dataset and the factor values and keys.
        /// </summary>
        /// <param name="name">Name of the dataset</param>
        /// <param name="keys">Factor values of </param>
        public FactorDataset(string name, Hashtable keys)
        {
            m_datasetName = name;
            m_factorKeys = keys;
        }

        /// <summary>
        ///     Gets/Sets index of the dataset in the array for ordering and sorting algorithms.
        /// </summary>
        public long Index
        {
            get { return m_index; }
            set { m_index = value; }
        }

        /// <summary>
        ///     Returns reference to underlying hashtable that holds the key-factors and value-factorvalue combinations.
        /// </summary>
        public Hashtable Values
        {
            get { return m_factorKeys; }
            set { m_factorKeys = value; }
        }

        /// <summary>
        ///     Returns the name of the dataset.
        /// </summary>
        public string Name
        {
            get { return m_datasetName; }
            set { m_datasetName = value; }
        }

        #region ICloneable Members

        public object Clone()
        {
            var newTable = new Hashtable();
            foreach (var o in m_factorKeys.Keys)
            {
                newTable.Add(o, m_factorKeys[o]);
            }

            var newData = new FactorDataset(Name, newTable);
            newData.Name = Name;
            newData.Index = Index;

            return newData;
        }

        #endregion
    }
}