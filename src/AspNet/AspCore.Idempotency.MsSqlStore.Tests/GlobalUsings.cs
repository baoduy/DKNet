// <copyright file="GlobalUsings.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

global using Xunit;
global using Shouldly;
global using DKNet.AspCore.Idempotency.Filtering;
global using DKNet.AspCore.Idempotency.Store;
global using DKNet.AspCore.Idempotency.MsSqlStore;
global using DKNet.AspCore.Idempotency.MsSqlStore.Data;
global using IdempotencyKeyEntity = DKNet.AspCore.Idempotency.Relational.Data.IdempotencyKeyEntity;
global using Microsoft.EntityFrameworkCore;