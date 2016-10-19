using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Breeze.PocoMetadata
{
    /// <summary>
    /// Builds a data structure containing the metadata required by Breeze.
    /// see http://www.breezejs.com/documentation/breeze-metadata-format 
    /// </summary>
    public class PocoMetadataBuilder
    {
        private Metadata _map;
        private List<Dictionary<string, object>> _typeList;
        private Dictionary<string, object> _resourceMap;
        private Dictionary<Type, Dictionary<string, object>> _typeMap;
        private List<Dictionary<string, object>> _enumList;
        private List<Type> _allTypes; // even those excluded or replaced
        private List<Type> _types;
        private List<Type> _entityTypes;
        private EntityDescriptor _describer;


        /// <summary>
        /// Create an instance using the given EntityDescriptor to resolve metadata
        /// </summary>
        /// <param name="describer"></param>
        public PocoMetadataBuilder(EntityDescriptor describer)
        {
            this._describer = describer;
        }

        /// <summary>
        /// Build the Breeze metadata as a nested Dictionary.  
        /// The result can be converted to JSON and sent to the Breeze client.
        /// </summary>
        /// <param name="types">Entity metadata types to include in the metadata</param>
        /// <returns></returns>
        public Metadata BuildMetadata(IEnumerable<Type> types)
        {
            InitMap();
            _allTypes = types.ToList();
            _types = types.Select(t => _describer.Replace(t, types)).Distinct().Where(t => _describer.Include(t)).ToList();
            _entityTypes = _types.Where(t => !_describer.IsComplexType(t)).ToList();
            var navigations = new List<Dictionary<string, object>>();

            foreach (var t in _types)
            {
                // Add type with data properties
                AddType(t);
            }
            foreach (var t in _types)
            {
                // Add navigation properties
                var cmap = _typeMap[t];
                var dataList = (List<Dictionary<string, object>>)cmap["dataProperties"];
                var navList = new List<Dictionary<string, object>>();
                AddNavigationProperties(t, dataList, navList);
                if (navList.Any())
                {
                    cmap.Add("navigationProperties", navList);
                    navigations.AddRange(navList);
                }
            }

            // resolve associations into association names
            ResolveAssociations(navigations);

            if (_enumList.Any())
            {
                _map.Add("enumTypes", _enumList);
            }
            return _map;
        }


        /// <summary>
        /// Populate the metadata header.
        /// </summary>
        void InitMap()
        {
            _map = new Metadata();
            _typeList = new List<Dictionary<string, object>>();
            _typeMap = new Dictionary<Type, Dictionary<string, object>>();
            _resourceMap = new Dictionary<string, object>();
            _map.ForeignKeyMap = new Dictionary<string, string>();
            _enumList = new List<Dictionary<string, object>>();
            _map.Add("localQueryComparisonOptions", "caseInsensitiveSQL");
            _map.Add("structuralTypes", _typeList);
            _map.Add("resourceEntityTypeMap", _resourceMap);
        }

        /// <summary>
        /// Add the metadata for an entity.
        /// </summary>
        /// <param name="type">Type for which metadata is being generated</param>
        void AddType(Type type)
        {
            // "Customer:#Breeze.Models.NorthwindIBModel": {
            var classKey = type.Name + ":#" + type.Namespace;
            var cmap = new Dictionary<string, object>();
            _typeList.Add(cmap);
            _typeMap.Add(type, cmap);

            cmap.Add("shortName", type.Name);
            cmap.Add("namespace", type.Namespace);
            if (!type.IsInterface)
            {
                var interfaces = type.GetInterfaces().Except(type.BaseType.GetInterfaces()).Where(t => _types.Contains(t)).Select(t => t.Name).ToList();
                if (interfaces.Any())
                {
                    var custom = new Dictionary<string, object>() { { "interfaces", string.Join(", ", interfaces) } };
                    cmap.Add("custom", custom);
                }
            }

            if (_describer.IsComplexType(type))
            {
                cmap.Add("isComplexType", true);
            }
            else
            {
                // Only identify the base type if it is also an entity in the type list
                if (_entityTypes.Contains(type.BaseType))
                {
                    var baseTypeName = type.BaseType.Name + ":#" + type.BaseType.Namespace;
                    cmap.Add("baseTypeName", baseTypeName);
                }

                if (type.IsAbstract)
                {
                    cmap.Add("isAbstract", true);
                }
                // Get the autoGeneratedKeyType for this type
                var keyGenerator = _describer.GetAutoGeneratedKeyType(type);
                if (keyGenerator != null)
                {
                    cmap.Add("autoGeneratedKeyType", keyGenerator);
                }

                var resourceName = _describer.GetResourceName(type);
                cmap.Add("defaultResourceName", resourceName);
                _resourceMap.Add(resourceName, classKey);
            }


            var dataList = new List<Dictionary<string, object>>();
            cmap.Add("dataProperties", dataList);

            AddDataProperties(type, dataList);

            // Change the autoGeneratedKeyType if an attribute was found on a data property
            var keyProp = FindEntry(dataList, "isPartOfKey");
            if (keyProp != null)
            {
                var custom = keyProp.Get("custom");
                if ("Identity".Equals(custom))
                {
                    cmap["autoGeneratedKeyType"] = "Identity";
                    keyProp.Remove("custom");
                }
                else if ("Computed".Equals(custom))
                {
                    cmap["autoGeneratedKeyType"] = "KeyGenerator";
                    keyProp.Remove("custom");
                }
            }
            else if (!type.IsAbstract && !type.IsEnum && !_describer.IsComplexType(type) && !_entityTypes.Contains(type.BaseType))
            {
                // No key for an entity => error or add the key
                var missingFKHandling = _describer.GetMissingPKHandling(type);
                if (missingFKHandling == MissingKeyHandling.Error)
                {
                    throw new Exception("Key not found for entity " + classKey);
                }
                else if (missingFKHandling == MissingKeyHandling.Add)
                {
                    var dmap = new Dictionary<string, object>();
                    dmap.Add("nameOnServer", type.Name + "GenKey");
                    dmap.Add("dataType", "Guid"); // TODO make this configurable
                    dmap.Add("isPartOfKey", true);
                    dmap.Add("custom", "pk_generated");
                    dataList.Add(dmap);
                }
                else
                {
                    Console.Error.WriteLine("Key not found for entity " + classKey);
                }
            }

        }

        /// <summary>
        /// Add the properties for an entity type.
        /// </summary>
        /// <param name="type">Type for which metadata is being generated</param>
        /// <param name="dataList">will be populated with the data properties of the entity</param>
        void AddDataProperties(Type type, List<Dictionary<string, object>> dataList)
        {
            // Get properties for the given class
            var propertyInfos = GetPropertyInfos(type);

            foreach (var propertyInfo in propertyInfos)
            {
                var elementType = GetElementType(propertyInfo.PropertyType);
                if (_entityTypes.Contains(elementType) || (_entityTypes.Contains(_describer.Replace(elementType, _allTypes))))
                {
                    // association to another entity in the metadata list; skip until later
                }
                else
                {
                    // data property
                    var isKey = _describer.IsKeyProperty(type, propertyInfo);
                    var isVersion = _describer.IsVersionProperty(type, propertyInfo);

                    var dmap = MakeDataProperty(type, propertyInfo, isKey, isVersion);
                    if (dmap == null) continue; // excluded
                    dataList.Add(dmap);

                    // add enum types to the global enum list
                    var realType = propertyInfo.PropertyType;
                    var types = propertyInfo.PropertyType.GetGenericArguments();
                    if (types.Length > 0) realType = types[0];
                    if (realType.IsEnum)
                    {
                        if (!_enumList.Exists(x => x.ContainsValue(realType.Name)))
                        {
                            string[] enumNames = Enum.GetNames(realType);
                            var p = new Dictionary<string, object>();
                            p.Add("shortName", realType.Name);
                            p.Add("namespace", realType.Namespace);
                            p.Add("values", enumNames);
                            _enumList.Add(p);
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Add the properties for an entity type.
        /// </summary>
        /// <param name="type">Type for which metadata is being generated</param>
        /// <param name="dataList">Already populated with the data properties of the entity</param>
        /// <param name="navList">will be populated with the navigation properties of the entity</param>
        void AddNavigationProperties(Type type, List<Dictionary<string, object>> dataList, List<Dictionary<string, object>> navList)
        {
            // Get properties for the given class
            var propertyInfos = GetPropertyInfos(type);

            // Process to handle the association properties
            foreach (var propertyInfo in propertyInfos)
            {
                var elementType = GetElementType(propertyInfo.PropertyType);
                if (_entityTypes.Contains(elementType) || (_entityTypes.Contains(_describer.Replace(elementType, _allTypes))))
                {
                    // now handle association to other entities
                    // navigation property
                    var isKey = _describer.IsKeyProperty(type, propertyInfo);
                    var assProp = MakeAssociationProperty(type, propertyInfo, dataList, isKey);
                    navList.Add(assProp);
                }
            }

            // now find & add missing nav properties identified by "__relatedType"
            foreach (var dp in dataList)
            {
                var relatedType = (Type) dp.Get("__relatedType");
                if (relatedType != null)
                {
                    relatedType = _describer.Replace(relatedType, _allTypes);
                    var nmap = new Dictionary<string, object>();
                    var name = (string) dp.Get("nameOnServer");
                    if (name.EndsWith("Id"))
                        name = name.Substring(0, name.Length - 2);
                    else
                        name = name + "Ref";

                    nmap.Add("nameOnServer", name);
                    nmap.Add("entityTypeName", relatedType.Name + ":#" + relatedType.Namespace);
                    nmap.Add("isScalar", true);

                    // Add assocation.  The associationName will be resolved later, when both ends are known.
                    nmap.Add("__association", new Association(type, relatedType, null));

                    var fkNames = new string[] { dp["nameOnServer"].ToString() };
                    nmap["foreignKeyNamesOnServer"] = fkNames;
                    nmap.Add("custom", "ref_generated"); // a clue to the client
                    navList.Add(nmap);

                    dp.Remove("__foreignKey");
                    dp.Remove("__relatedType");

                    // For many-to-one and one-to-one associations, save the relationship in ForeignKeyMap for re-establishing relationships during save
                    var entityRelationship = type.FullName + '.' + name;
                    _map.ForeignKeyMap.Add(entityRelationship, string.Join(",", fkNames));

                }
            }
        }

        /// <summary>
        /// Get the PropertyInfo definitions for the properties on the given type
        /// Filter out properties that are defined on a base class that is also in the metadata
        /// </summary>
        /// <param name="type">Type for which to get the properties</param>
        /// <returns></returns>
        private PropertyInfo[] GetPropertyInfos(Type type)
        {
            // Get properties for the given class
            var propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Exclude properties that are declared on a base class that is also in the type list
            // those properties will be defined in the metadata for the base class
            propertyInfos = propertyInfos.Where(p =>
            {
                if (!_entityTypes.Contains(p.DeclaringType)) return true;

                if (!p.DeclaringType.Equals(type)) return false;

                var getMethod = p.GetGetMethod(false);
                //if (getMethod.IsAbstract) return false;
                // Exclude overriding properties; they will be defined in the metadata for the base class
                var baseMethod = getMethod.GetBaseDefinition();
                if (baseMethod.DeclaringType == getMethod.DeclaringType) return true;
                    //|| (baseMethod.IsAbstract && baseMethod.DeclaringType == type.BaseType)) return true;
                return false;
            }).ToArray();

            return propertyInfos;
        }

        /// <summary>
        /// Make data property metadata for the entity property.  
        /// Attributes one the property are used to set some metadata values.
        /// </summary>
        /// <param name="propertyInfo">Property info for the property</param>
        /// <param name="containingType">Type containing the property</param>
        /// <param name="isKey">true if this property is part of the key for the entity</param>
        /// <param name="isVersion">true if this property contains the version of the entity (for a concurrency strategy)</param>
        /// <returns>Dictionary of metadata for the property</returns>
        private Dictionary<string, object> MakeDataProperty(Type containingType, PropertyInfo propertyInfo, bool isKey, bool isVersion)
        {
            var propType = _describer.GetDataPropertyType(containingType, propertyInfo);
            if (propType == null) return null; // exclude this property

            var nullableType = Nullable.GetUnderlyingType(propType);
            var isNullable = nullableType != null || !propType.IsValueType;
            propType = nullableType ?? propType;


            var dmap = new Dictionary<string, object>();
            dmap.Add("nameOnServer", propertyInfo.Name);
            var elementType = GetElementType(propType);

            if (_describer.IsComplexType(elementType))
            {
                dmap.Add("complexTypeName", elementType.Name + ":#" + elementType.Namespace);
            }
            else
            {
                dmap.Add("dataType", elementType.Name);
            }

            if (elementType != propType)
                dmap.Add("isScalar", false);

            if (!isNullable) dmap.Add("isNullable", false);

            AddAttributesToDataProperty(propertyInfo, dmap);

            if (isKey) dmap["isPartOfKey"] = true;
            if (isVersion) dmap["concurrencyMode"] = "Fixed";
            if (propType.IsEnum)
            {
                dmap["dataType"] = "String";
                dmap["enumType"] = propType.Name;
            }

            var validators = (List<Dictionary<string, object>>)dmap.Get("validators");
            if (validators == null) validators = new List<Dictionary<string, object>>();

            if (!isNullable)
            {
                var already = FindEntry(validators, "name", "required");
                if (already == null)
                    validators.Add(new Dictionary<string, object>() {{"name", "required" }});
            }

            string validationType;
            if (ValidationTypeMap.TryGetValue(propType.Name, out validationType))
            {
                validators.Add(new Dictionary<string, object>() { {"name", validationType }});
            }
            if (validators.Any())
                dmap["validators"] = validators;

            return dmap;
        }

        /// <summary>
        /// Make association property metadata for the entity.
        /// Also populates the ForeignKeyMap which is used for related-entity fixup in NHContext.FixupRelationships
        /// </summary>
        /// <param name="propertyInfo">Property info describing the property</param>
        /// <param name="containingType">Type containing the property</param>
        /// <param name="dataProperties">Data properties already collected for the containingType.  "isPartOfKey" may be added to a property.</param>
        /// <param name="isKey">Whether the property is part of the key</param>
        /// <returns></returns>
        private Dictionary<string, object> MakeAssociationProperty(Type containingType, PropertyInfo propertyInfo, List<Dictionary<string, object>> dataProperties, bool isKey)
        {
            var nmap = new Dictionary<string, object>();
            var name = propertyInfo.Name;
            nmap.Add("nameOnServer", name);

            var propType = propertyInfo.PropertyType;
            var isCollection = IsCollectionType(propType);
            var relatedEntityType = isCollection ? GetElementType(propType) : propType;
            relatedEntityType = _describer.Replace(relatedEntityType, _allTypes);
            nmap.Add("entityTypeName", relatedEntityType.Name + ":#" + relatedEntityType.Namespace);
            nmap.Add("isScalar", !isCollection);

            AddAttributesToNavProperty(propertyInfo, nmap);

            var entityRelationship = containingType.FullName + '.' + name;

            // Add assocation.  The associationName will be resolved later, when both ends are known.
            nmap.Add("__association", new Association(containingType, relatedEntityType, null));


            if (!isCollection)
            {
                // For scalar navigation properties, we need to identify the foreign key
                var missingFKHandling = _describer.GetMissingFKHandling(containingType, propertyInfo);

                Dictionary<string, object> dataProp = null;
                // Find the matching key in the data properties for this entity
                // First see if a data property was identified on an attribute of the navigation property
                object fkNames = nmap.Get("foreignKeyNamesOnServer");
                if (fkNames != null)
                {
                    // find the matching data prop using its fk name
                    var fkName = ((string[])fkNames)[0];
                    dataProp = FindEntry(dataProperties, "nameOnServer", fkName);
                }
                if (dataProp == null)
                {
                    // Next see if a data property was marked as a foreign key during attribute processing
                    dataProp = FindEntry(dataProperties, "__foreignKey", name);
                }
                if (dataProp == null)
                {
                    // Use the descriptor to guess the foreign key
                    var dataPropertyName = _describer.GetForeignKeyName(containingType, propertyInfo);
                    if (dataPropertyName != null)
                    {
                        dataProp = FindEntry(dataProperties, "nameOnServer", dataPropertyName);
                    }
                }
                if (dataProp == null && missingFKHandling == MissingKeyHandling.Add)
                {
                    // Add a new dataproperty to represent the foreign key
                    var dataPropertyName = _describer.GetForeignKeyName(containingType, propertyInfo);
                    dataProp = new Dictionary<string, object>();
                    dataProp.Add("nameOnServer", dataPropertyName);
                    dataProp.Add("custom", "fk_generated");

                    string dataType = null;
                    // find the related entity so we can get the datatype of the key
                    var relatedEntityProperties = relatedEntityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    var relatedKey = relatedEntityProperties.Where(p => _describer.IsKeyProperty(relatedEntityType, p)).FirstOrDefault();
                    if (relatedKey != null)
                    {
                        dataType = _describer.GetDataPropertyType(relatedEntityType, relatedKey).Name;
                    }
                    if (dataType == null) dataType = "Guid";
                    dataProp.Add("dataType", dataType);
                    dataProperties.Add(dataProp);
                }

                if (dataProp != null)
                {
                    fkNames = new string[] { dataProp["nameOnServer"].ToString() };
                    nmap["foreignKeyNamesOnServer"] = fkNames;
                    dataProp.Remove("__foreignKey");
                    dataProp.Remove("__relatedType");

                    // if the navigation property is defined as part of the key, set the fk data property instead
                    if (isKey) dataProp["isPartOfKey"] = true;

                    // For many-to-one and one-to-one associations, save the relationship in ForeignKeyMap for re-establishing relationships during save
                    _map.ForeignKeyMap.Add(entityRelationship, string.Join(",", fkNames));
                }
                else
                {
                    if (missingFKHandling == MissingKeyHandling.Error)
                    {
                        throw new Exception("Cannot find foreign key property on type " + containingType.Name + " for navigation property " + propertyInfo.Name);
                    }
                    else if (missingFKHandling == MissingKeyHandling.Log)
                    {
                        Console.Error.WriteLine("Cannot find foreign key property on type " + containingType.Name + " for navigation property " + propertyInfo.Name);
                        _map.ForeignKeyMap.Add(entityRelationship, "ERROR - NOT FOUND");
                    }
                }

            }

            return nmap;
        }

        /// <summary>
        /// Resolve the association names for the navigation properties.
        /// Try to find common names for both ends of an association
        /// </summary>
        /// <param name="navigations"></param>
        static void ResolveAssociations(List<Dictionary<string, object>> navigations)
        {
            foreach (var nav in navigations)
            {
                if (nav.ContainsKey("associationName")) continue; // skip if already processed
                var unmatched = navigations.Where(n => n != nav && (!n.ContainsKey("associationName")));

                var ass = (Association)nav["__association"];

                var inverse = unmatched.Where(n => {
                    var na = (Association)n["__association"];
                    return ass.TypesEqual(na.containingType, na.relatedType, na.fkNames);
                }).FirstOrDefault();

                if (inverse == null)
                {
                    inverse = unmatched.Where(n => {
                        var na = (Association)n["__association"];
                        return ass.TypesEqual(na.containingType, na.relatedType.BaseType, na.fkNames);
                    }).FirstOrDefault();
                }

                if (inverse == null)
                {
                    inverse = unmatched.Where(n => {
                        var na = (Association)n["__association"];
                        return na.TypesEqual(ass.containingType, ass.relatedType.BaseType, na.fkNames);
                    }).FirstOrDefault();
                }

                // TODO: handle interfaces

                if (inverse != null)
                {
                    var iass = (Association)inverse["__association"];
                    var associationName = GetAssociationName(iass.containingType, iass.relatedType, iass.fkNames);
                    inverse["associationName"] = associationName;
                    nav["associationName"] = associationName;
                    inverse.Remove("__association");
                    nav.Remove("__association");
                }
            }

            // Set association names for unmatched association
            foreach (var nav in navigations)
            {
                if (nav.ContainsKey("associationName")) continue; // skip if already processed
                var ass = (Association)nav["__association"];
                var associationName = GetAssociationName(ass.containingType, ass.relatedType, ass.fkNames);
                nav["associationName"] = associationName;
                nav.Remove("__association");
                var cust = (string)nav.Get("custom");
                cust = (cust != null) ? cust + ", one-way" : "one-way";
                nav["custom"] = cust;
            }

        }


        /// <summary>
        /// Add to the data property map based on attributes on the class member.  Checks a list of known annotations.
        /// </summary>
        /// <param name="memberInfo">Property or field of the class for which metadata is being generated</param>
        /// <param name="dmap">Data property definition</param>
        private static void AddAttributesToDataProperty(MemberInfo memberInfo, Dictionary<string, object> dmap)
        {
            var validators = new List<Dictionary<string, object>>();
            var attributes = memberInfo.GetCustomAttributes();
            foreach (var attr in attributes)
            {
                var name = attr.GetType().Name;
                if (name.EndsWith("Attribute"))
                {
                    // get the name without "Attribute" on the end
                    name = name.Substring(0, name.Length - "Attribute".Length);
                }

                if (name == "Key" || name == "PrimaryKey")
                {
                    dmap["isPartOfKey"] = true;
                }
                else if (name == "ConcurrencyCheck")
                {
                    dmap["concurrencyMode"] = "Fixed";
                }
                else if (name == "Required")
                {
                    dmap["isNullable"] = false;
                    var validator = new Dictionary<string, object>() { { "name", "required" } };
                    validators.Add(validator);
                }
                else if (name == "DefaultValue")
                {
                    dmap["defaultValue"] = GetAttributeValue(attr, "Value");
                }
                else if (name == "MaxLength")
                {
                    var max = GetAttributeValue(attr, "Length");
                    dmap["maxLength"] = max;
                    var validator = new Dictionary<string, object>() { { "name", "maxLength" }, { "maxLength", max } };
                    validators.Add(validator);
                }
                else if (name == "StringLength")
                {
                    // ServiceStack [StringLength(max, min)]
                    var max = GetAttributeValue(attr, "MaximumLength");
                    dmap["maxLength"] = max;
                    var validator = new Dictionary<string, object>() { { "name", "maxLength" }, { "maxLength", max } };
                    validators.Add(validator);
                    var min = (int) GetAttributeValue(attr, "MinimumLength");
                    if (min > 0)
                    {
                        dmap["minLength"] = min;
                        var minValidator = new Dictionary<string, object>() { { "name", "minLength" }, { "minLength", min } };
                        validators.Add(minValidator);
                    }
                }
                else if (name == "DatabaseGenerated")
                {
                    var opt = GetAttributeValue(attr, "DatabaseGeneratedOption").ToString();
                    if (opt != "None") dmap["custom"] = opt;
                }
                else if (name == "ForeignKey")
                {
                    var relatedType = GetAttributeValue(attr, "Type");
                    if (relatedType != null)
                    {
                        // ServiceStack: foreign key points to related type
                        dmap["__relatedType"] = relatedType;
                    }
                    else
                    {
                        // DataAnnotation: ForeignKey points to navigation property name
                        // will be resolved & removed while processing navigation properties
                        dmap["__foreignKey"] = GetAttributeValue(attr, "Name");
                    }
                }
                else if (name == "References")
                {
                    // ServiceStack: like a foreign key, but no actual db fk relationship exists
                    var relatedType = GetAttributeValue(attr, "Type");
                    if (relatedType != null)
                    {
                        // ServiceStack: foreign key points to related type
                        dmap["__relatedType"] = relatedType;
                    }
                }
                else if (name == "InverseProperty")
                {
                    dmap["custom"] = new Dictionary<string, object> { { "inverseProperty", GetAttributeValue(attr, "Property") } };
                }
                else if (name.Contains("Validat"))
                {
                    // Assume some sort of validator.  Add all the properties of the attribute to the validation map
                    // TODO - this only works if the custom validator is registered on the Breeze client.  Otherwise it throws an error.
                    //var validator = new Dictionary<string, object>() { { "name", camelCase(name) } };
                    //validators.Add(validator);
                    //foreach (var propertyInfo in attr.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.FlattenHierarchy))
                    //{
                    //    var value = propertyInfo.GetValue(attr);
                    //    if (value != null)
                    //    {
                    //        validator[camelCase(propertyInfo.Name)] = value;
                    //    }
                    //}
                }
            }

            if (validators.Any())
            {
                dmap.Add("validators", validators);
            }

        }

        /// <summary>
        /// Add to the navigation property map based on attributes on the class member.  Checks a list of known annotations.
        /// </summary>
        /// <param name="memberInfo">Property or field of the class for which metadata is being generated</param>
        /// <param name="nmap">Navigation property definition</param>
        private static void AddAttributesToNavProperty(MemberInfo memberInfo, Dictionary<string, object> nmap)
        {
            var attributes = memberInfo.GetCustomAttributes();
            foreach (var attr in attributes)
            {
                var name = attr.GetType().Name;
                if (name.EndsWith("Attribute"))
                {
                    // get the name without "Attribute" on the end
                    name = name.Substring(0, name.Length - "Attribute".Length);
                }

                if (name == "ForeignKey")
                {
                    var names = new string[] { GetAttributeValue(attr, "Name").ToString() };
                    nmap["foreignKeyNamesOnServer"] = names;
                }
                else if (name == "InverseProperty")
                {
                    nmap["custom"] = new Dictionary<string, object> { { "inverseProperty", GetAttributeValue(attr, "Property") } };
                }
                else if (name == "Reference")
                {
                    // ServiceStack: attribute indicates a navigation property
                }
            }
        }

        /// <summary>
        /// Find an dictionary in the list that has a property matching the value. 
        /// Matches are performed by converting values to string and doing case-insensitive comparison.
        /// </summary>
        /// <param name="entries">List of dictionaries to search</param>
        /// <param name="propertyName">Name of property in each dictionary that will be compared</param>
        /// <param name="value">Value to compare against.  If null, then any non-null value of property is a match.</param>
        /// <returns>First entry for which entry[propertyName] == value, ignoring case.</returns>
        private static Dictionary<string, object> FindEntry(List<Dictionary<string, object>> entries, string propertyName, string value = null)
        {
            return entries.Where(e => {
                var propValue = e.Get(propertyName);
                if (propValue == null) return false;
                if (value == null) return true;
                return string.Equals(value, propValue.ToString(), StringComparison.OrdinalIgnoreCase);
            }).FirstOrDefault();
        }

        /// <summary>
        /// Get the value of the given property of the attribute
        /// </summary>
        /// <param name="attr">Attribute to inspect</param>
        /// <param name="propertyName">Name of property</param>
        /// <returns></returns>
        private static object GetAttributeValue(Attribute attr, string propertyName)
        {
            var propertyInfo = attr.GetType().GetProperty(propertyName);
            if (propertyInfo == null) return null;
            var value = propertyInfo.GetValue(attr);
            return value;
        }

        /// <summary>
        /// Return true if the type represents an array or enumerable type, false otherwise.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool IsCollectionType(Type type)
        {
            return type == typeof(Array)
                || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                || type.GetInterfaces().Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        }

        /// <summary>
        /// Return the element type of a collection type (array or IEnumerable)
        /// For a plain IEnumerable, return System.Object
        /// For a non-collection type, return the given type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static Type GetElementType(Type type)
        {
            if (type == typeof(String)) return type;
            if (!IsCollectionType(type)) return type;
            if (type.HasElementType) return type.GetElementType();
            if (type.IsGenericType)
            {
                var args = type.GetGenericArguments();
                return args[args.Length - 1];
            }
            return typeof(object);
        }

        /// <summary>
        /// Creates an association name from two entity names.
        /// For consistency, puts the entity names in alphabetical order.
        /// </summary>
        /// <param name="type1">Containing type</param>
        /// <param name="type2">Related type</param>
        /// <param name="fkNames">Used to ensure the association name is unique for a type</param>
        /// <returns></returns>
        static string GetAssociationName(Type type1, Type type2, string[] fkNames)
        {
            if (type1 == null || type2 == null) return null;
            string name1 = type1.Name;
            string name2 = type2.Name;
            var cols = (fkNames != null) ? "_" + string.Join(" ", fkNames) : "";
            if (name1.CompareTo(name2) < 0)
                return FK + name1 + '_' + name2 + cols;
            else
                return FK + name2 + '_' + name1 + cols;
        }
        const string FK = "AN_";

        /// <summary>
        /// Change first letter to lowercase
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private string camelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0]))
            {
                return s;
            }
            string str = char.ToLower(s[0]).ToString();
            if (s.Length > 1)
            {
                str = str + s.Substring(1);
            }
            return str;

        }

        // Map of data type to Breeze validation type
        static Dictionary<string, string> ValidationTypeMap = new Dictionary<string, string>() {
                    {"Boolean", "bool" },
                    {"Byte", "byte" },
                    {"DateTime", "date" },
                    {"DateTimeOffset", "date" },
                    {"Decimal", "number" },
                    {"Guid", "guid" },
                    {"Int16", "int16" },
                    {"Int32", "int32" },
                    {"Int64", "integer" },
                    {"Single", "number" },
                    {"Time", "duration" },
                    {"TimeSpan", "duration" },
                    {"TimeAsTimeSpan", "duration" }
                };

    }



    /// <summary>
    /// Metadata describing the entity model.  Converted to JSON to send to Breeze client.
    /// </summary>
    public class Metadata : Dictionary<string, object>
    {
        /// <summary>
        /// Map of relationship name -> foreign key name, e.g. "Customer" -> "CustomerID".
        /// Used for re-establishing the entity relationships from the foreign key values during save.
        /// This part is not sent to the client because it is separate from the base dictionary implementation.
        /// </summary>
        public IDictionary<string, string> ForeignKeyMap;
    }

    /// <summary>
    /// Represents an association (navigation property) between two types.
    /// This is used to create the association name in the metadata.
    /// </summary>
    class Association
    {
        public Association(Type type, Type related, string[] fkNames = null)
        {
            this.containingType = type;
            this.relatedType = related;
            this.fkNames = fkNames;
        }
        public Type containingType;
        public Type relatedType;
        public string[] fkNames;

        /// <summary>
        /// Return true if the types match or their base types match
        /// </summary>
        /// <returns></returns>
        public bool TypesEqual(Type t1, Type t2, string[] fkNames)
        {
            var a1 = new Association(t1, t2, fkNames);
            return this.Equals(a1);
        }

        public override bool Equals(object obj)
        {
            var assn = obj as Association;
            if (assn == null) return false;
            if (assn.containingType == null || assn.relatedType == null) return false;
            // equal even if the types are reversed
            if (((assn.containingType == this.containingType) &&
                (assn.relatedType == this.relatedType)) || 
                ((assn.containingType == this.relatedType) &&
                (assn.relatedType == this.containingType)))
            {
                if (assn.fkNames == this.fkNames) return true;
                if (assn.fkNames == null || this.fkNames == null) return false;
                if (string.Join(",", assn.fkNames).Equals(string.Join(",", this.fkNames))) return true;
                return false;
            }
            return false;
        }

        public override int GetHashCode()
        {
            var h1 = containingType != null ? containingType.GetHashCode() : 0;
            var h2 = relatedType != null ? relatedType.GetHashCode() : 0;
            var h3 = fkNames != null ? fkNames.GetHashCode() : 0;
            return h1/3 + h2/3 + h3/3;
        }
    }
}
