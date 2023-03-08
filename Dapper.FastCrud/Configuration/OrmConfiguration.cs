﻿// ReSharper disable once CheckNamespace (the namespace is intentionally not in sync with the file location) 
namespace Dapper.FastCrud
{
    using System;
    using System.Runtime.CompilerServices;
    using Dapper.FastCrud.Configuration;
    using Dapper.FastCrud.EntityDescriptors;
    using Dapper.FastCrud.Mappings;
    using Dapper.FastCrud.Mappings.Registrations;
    using Dapper.FastCrud.Validations;
    using System.Collections.Generic;

    /// <summary>
    /// Sets up various FastCrud settings.
    /// </summary>
    public static class OrmConfiguration
    {
        private static volatile SqlDialect _currentDefaultDialect = SqlDialect.MsSql;
        private static volatile OrmConventions _currentOrmConventions = new OrmConventions();
        // UPDATE: concurrent dictionaries are super slow in our case for the framework we're normally targeting, use regular locking methods instead
        private static readonly Dictionary<Type, EntityDescriptor> _entityDescriptorCache = new Dictionary<Type, EntityDescriptor>();
        private static readonly object _entityDescriptorCacheSyncLock = new object();
        private static volatile SqlStatementOptions _defaultStatementOptions = new SqlStatementOptions();

        /// <summary>
        /// Gets the default command options. 
        /// </summary>
        public static SqlStatementOptions DefaultSqlStatementOptions => _defaultStatementOptions;

        /// <summary>
        /// Gets or sets the default dialect. 
        /// </summary>
        public static SqlDialect DefaultDialect
        {
            get => _currentDefaultDialect;
            set => _currentDefaultDialect = value;
        }

        /// <summary>
        /// Gets or sets the conventions used by the library. Subclass <see cref="OrmConventions"/> to provide your own set of conventions.
        /// </summary>
        public static OrmConventions Conventions
        {
            get => _currentOrmConventions;
            set
            {
                Validate.NotNull(value, nameof(Conventions));
                _currentOrmConventions = value;
            }
        }

        /// <summary>
        /// Clears all the recorded entity registrations and entity ORM mappings.
        /// </summary>
        public static void ClearEntityRegistrations()
        {
            lock (_entityDescriptorCacheSyncLock)
            {
                _entityDescriptorCache.Clear();
            }
        }

        /// <summary>
        /// Returns the default entity mapping for an entity.
        /// This was either previously set by you in a call to <see cref="SetDefaultEntityMapping{TEntity}"/> or it was auto-generated by the library.
        /// 
        /// You can use the returned mappings to create new temporary mappings for the query calls or to override the defaults.
        /// Once the mappings have been used in query calls, the instance will be frozen and it won't support further modifications, but you can always call <see cref="EntityMapping{TEntity}.Clone"/> to create a new instance.
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        public static EntityMapping<TEntity> GetDefaultEntityMapping<TEntity>()
        {
            var entityDescriptor = GetEntityDescriptor<TEntity>();
            return new EntityMapping<TEntity>(entityDescriptor.CurrentEntityMappingRegistration);
        }

        /// <summary>
        /// Registers a new entity. Please continue setting up property mappings and other entity options with the returned default entity mapping instance.
        /// Remember that multiple instances of mappings for a single entity can be active at any time for a single entity type,
        /// but also multiple entities pointing to the same database table having different mappings.
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        public static EntityMapping<TEntity> RegisterEntity<TEntity>()
        {
            var entityRegistration = new EntityRegistration(typeof(TEntity));
            return SetDefaultEntityMapping(new EntityMapping<TEntity>(entityRegistration));
        }

        /// <summary>
        /// Sets the default entity type mapping for the entity type.
        /// This must be called before any query operations were made on the entity.
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        public static EntityMapping<TEntity> SetDefaultEntityMapping<TEntity>(EntityMapping<TEntity> mappings)
        {
            Validate.NotNull(mappings, nameof(mappings));
            var mappingRegistration = mappings.Registration;
            Validate.Argument(!mappingRegistration.IsFrozen, nameof(mappings), "The entity mappings were frozen and can't be used as defaults. They must be cloned first.");

            var entityRegistration = GetEntityDescriptor<TEntity>();
            entityRegistration.CurrentEntityMappingRegistration = mappingRegistration;
            return mappings;
        }

        /// <summary>
        /// Returns an SQL builder helpful for constructing verbatim SQL queries.
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        /// <param name="entityMapping">If NULL, de default entity mapping will be used.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ISqlBuilder GetSqlBuilder<TEntity>(EntityMapping<TEntity>? entityMapping = null)
        {
            return GetEntityDescriptor<TEntity>().GetSqlBuilder(entityMapping?.Registration);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static EntityDescriptor<TEntity> GetEntityDescriptor<TEntity>()
        {
            EntityDescriptor<TEntity> typedEntityDescriptor;
            var entityType = typeof(TEntity);
            lock (_entityDescriptorCacheSyncLock)
            {
                if (_entityDescriptorCache.TryGetValue(entityType, out EntityDescriptor genericEntityDescriptor))
                {
                    typedEntityDescriptor = (EntityDescriptor<TEntity>)genericEntityDescriptor;
                }
                else
                {
                    typedEntityDescriptor = new EntityDescriptor<TEntity>();
                    _entityDescriptorCache.Add(entityType, typedEntityDescriptor);
                }
            }

            return typedEntityDescriptor;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal static ISqlStatements<TEntity> GetSqlStatements<TEntity>(EntityRegistration? entityMapping = null)
        //{
        //    return (ISqlStatements<TEntity>)GetEntityDescriptor<TEntity>().GetSqlStatements(entityMapping);
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal static ISqlBuilder GetSqlBuilder<TEntity>(EntityRegistration? entityMapping = null)
        //{
        //    return GetEntityDescriptor<TEntity>().GetSqlStatements(entityMapping).SqlBuilder;
        //}

    }
}
