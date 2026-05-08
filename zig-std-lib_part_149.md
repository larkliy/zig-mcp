```
.pointer_stability.assertUnlocked();
            self.deallocate(allocator);
            self.* = undefined;
        }

        fn capacityForSize(size: Size) Size {
            var new_cap: u32 = @intCast((@as(u64, size) * 100) / max_load_percentage + 1);
            new_cap = math.ceilPowerOfTwo(u32, new_cap) catch unreachable;
            return new_cap;
        }

        pub fn ensureTotalCapacity(self: *Self, allocator: Allocator, new_size: Size) Allocator.Error!void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call ensureTotalCapacityContext instead.");
            return ensureTotalCapacityContext(self, allocator, new_size, undefined);
        }
        pub fn ensureTotalCapacityContext(self: *Self, allocator: Allocator, new_size: Size, ctx: Context) Allocator.Error!void {
            self.pointer_stability.lock();
            defer self.pointer_stability.unlock();
            if (new_size > self.size)
                try self.growIfNeeded(allocator, new_size - self.size, ctx);
        }

        pub fn ensureUnusedCapacity(self: *Self, allocator: Allocator, additional_size: Size) Allocator.Error!void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call ensureUnusedCapacityContext instead.");
            return ensureUnusedCapacityContext(self, allocator, additional_size, undefined);
        }
        pub fn ensureUnusedCapacityContext(self: *Self, allocator: Allocator, additional_size: Size, ctx: Context) Allocator.Error!void {
            return ensureTotalCapacityContext(self, allocator, self.count() + additional_size, ctx);
        }

        pub fn clearRetainingCapacity(self: *Self) void {
            self.pointer_stability.lock();
            defer self.pointer_stability.unlock();
            if (self.metadata) |_| {
                self.initMetadatas();
                self.size = 0;
                self.available = @truncate((self.capacity() * max_load_percentage) / 100);
            }
        }

        pub fn clearAndFree(self: *Self, allocator: Allocator) void {
            self.pointer_stability.lock();
            defer self.pointer_stability.unlock();
            self.deallocate(allocator);
            self.size = 0;
            self.available = 0;
        }

        pub fn count(self: Self) Size {
            return self.size;
        }

        fn header(self: Self) *Header {
            return @ptrCast(@as([*]Header, @ptrCast(@alignCast(self.metadata.?))) - 1);
        }

        fn keys(self: Self) [*]K {
            return self.header().keys;
        }

        fn values(self: Self) [*]V {
            return self.header().values;
        }

        pub fn capacity(self: Self) Size {
            if (self.metadata == null) return 0;

            return self.header().capacity;
        }

        pub fn iterator(self: *const Self) Iterator {
            return .{ .hm = self };
        }

        pub fn keyIterator(self: Self) KeyIterator {
            if (self.metadata) |metadata| {
                return .{
                    .len = self.capacity(),
                    .metadata = metadata,
                    .items = self.keys(),
                };
            } else {
                return .{
                    .len = 0,
                    .metadata = undefined,
                    .items = undefined,
                };
            }
        }

        pub fn valueIterator(self: Self) ValueIterator {
            if (self.metadata) |metadata| {
                return .{
                    .len = self.capacity(),
                    .metadata = metadata,
                    .items = self.values(),
                };
            } else {
                return .{
                    .len = 0,
                    .metadata = undefined,
                    .items = undefined,
                };
            }
        }

        /// Insert an entry in the map. Assumes it is not already present.
        pub fn putNoClobber(self: *Self, allocator: Allocator, key: K, value: V) Allocator.Error!void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call putNoClobberContext instead.");
            return self.putNoClobberContext(allocator, key, value, undefined);
        }
        pub fn putNoClobberContext(self: *Self, allocator: Allocator, key: K, value: V, ctx: Context) Allocator.Error!void {
            {
                self.pointer_stability.lock();
                defer self.pointer_stability.unlock();
                try self.growIfNeeded(allocator, 1, ctx);
            }
            self.putAssumeCapacityNoClobberContext(key, value, ctx);
        }

        /// Asserts there is enough capacity to store the new key-value pair.
        /// Clobbers any existing data. To detect if a put would clobber
        /// existing data, see `getOrPutAssumeCapacity`.
        pub fn putAssumeCapacity(self: *Self, key: K, value: V) void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call putAssumeCapacityContext instead.");
            return self.putAssumeCapacityContext(key, value, undefined);
        }
        pub fn putAssumeCapacityContext(self: *Self, key: K, value: V, ctx: Context) void {
            const gop = self.getOrPutAssumeCapacityContext(key, ctx);
            gop.value_ptr.* = value;
        }

        /// Insert an entry in the map. Assumes it is not already present,
        /// and that no allocation is needed.
        pub fn putAssumeCapacityNoClobber(self: *Self, key: K, value: V) void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call putAssumeCapacityNoClobberContext instead.");
            return self.putAssumeCapacityNoClobberContext(key, value, undefined);
        }
        pub fn putAssumeCapacityNoClobberContext(self: *Self, key: K, value: V, ctx: Context) void {
            assert(!self.containsContext(key, ctx));

            const hash: Hash = ctx.hash(key);
            const mask = self.capacity() - 1;
            var idx: usize = @truncate(hash & mask);

            var metadata = self.metadata.? + idx;
            while (metadata[0].isUsed()) {
                idx = (idx + 1) & mask;
                metadata = self.metadata.? + idx;
            }

            assert(self.available > 0);
            self.available -= 1;

            const fingerprint = Metadata.takeFingerprint(hash);
            metadata[0].fill(fingerprint);
            self.keys()[idx] = key;
            self.values()[idx] = value;

            self.size += 1;
        }

        /// Inserts a new `Entry` into the hash map, returning the previous one, if any.
        pub fn fetchPut(self: *Self, allocator: Allocator, key: K, value: V) Allocator.Error!?KV {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call fetchPutContext instead.");
            return self.fetchPutContext(allocator, key, value, undefined);
        }
        pub fn fetchPutContext(self: *Self, allocator: Allocator, key: K, value: V, ctx: Context) Allocator.Error!?KV {
            const gop = try self.getOrPutContext(allocator, key, ctx);
            var result: ?KV = null;
            if (gop.found_existing) {
                result = KV{
                    .key = gop.key_ptr.*,
                    .value = gop.value_ptr.*,
                };
            }
            gop.value_ptr.* = value;
            return result;
        }

        /// Inserts a new `Entry` into the hash map, returning the previous one, if any.
        /// If insertion happens, asserts there is enough capacity without allocating.
        pub fn fetchPutAssumeCapacity(self: *Self, key: K, value: V) ?KV {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call fetchPutAssumeCapacityContext instead.");
            return self.fetchPutAssumeCapacityContext(key, value, undefined);
        }
        pub fn fetchPutAssumeCapacityContext(self: *Self, key: K, value: V, ctx: Context) ?KV {
            const gop = self.getOrPutAssumeCapacityContext(key, ctx);
            var result: ?KV = null;
            if (gop.found_existing) {
                result = KV{
                    .key = gop.key_ptr.*,
                    .value = gop.value_ptr.*,
                };
            }
            gop.value_ptr.* = value;
            return result;
        }

        /// If there is an `Entry` with a matching key, it is deleted from
        /// the hash map, and then returned from this function.
        pub fn fetchRemove(self: *Self, key: K) ?KV {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call fetchRemoveContext instead.");
            return self.fetchRemoveContext(key, undefined);
        }
        pub fn fetchRemoveContext(self: *Self, key: K, ctx: Context) ?KV {
            return self.fetchRemoveAdapted(key, ctx);
        }
        pub fn fetchRemoveAdapted(self: *Self, key: anytype, ctx: anytype) ?KV {
            if (self.getIndex(key, ctx)) |idx| {
                const old_key = &self.keys()[idx];
                const old_val = &self.values()[idx];
                const result = KV{
                    .key = old_key.*,
                    .value = old_val.*,
                };
                self.metadata.?[idx].remove();
                old_key.* = undefined;
                old_val.* = undefined;
                self.size -= 1;
                self.available += 1;
                return result;
            }

            return null;
        }

        /// Find the index containing the data for the given key.
        fn getIndex(self: Self, key: anytype, ctx: anytype) ?usize {
            if (self.size == 0) {
                // We use cold instead of unlikely to force a jump to this case,
                // no matter the weight of the opposing side.
                @branchHint(.cold);
                return null;
            }

            // If you get a compile error on this line, it means that your generic hash
            // function is invalid for these parameters.
            const hash: Hash = ctx.hash(key);

            const mask = self.capacity() - 1;
            const fingerprint = Metadata.takeFingerprint(hash);
            // Don't loop indefinitely when there are no empty slots.
            var limit = self.capacity();
            var idx = @as(usize, @truncate(hash & mask));

            var metadata = self.metadata.? + idx;
            while (!metadata[0].isFree() and limit != 0) {
                if (metadata[0].isUsed() and metadata[0].fingerprint == fingerprint) {
                    const test_key = &self.keys()[idx];

                    if (ctx.eql(key, test_key.*)) {
                        return idx;
                    }
                }

                limit -= 1;
                idx = (idx + 1) & mask;
                metadata = self.metadata.? + idx;
            }

            return null;
        }

        pub fn getEntry(self: Self, key: K) ?Entry {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getEntryContext instead.");
            return self.getEntryContext(key, undefined);
        }
        pub fn getEntryContext(self: Self, key: K, ctx: Context) ?Entry {
            return self.getEntryAdapted(key, ctx);
        }
        pub fn getEntryAdapted(self: Self, key: anytype, ctx: anytype) ?Entry {
            if (self.getIndex(key, ctx)) |idx| {
                return Entry{
                    .key_ptr = &self.keys()[idx],
                    .value_ptr = &self.values()[idx],
                };
            }
            return null;
        }

        /// Insert an entry if the associated key is not already present, otherwise update preexisting value.
        pub fn put(self: *Self, allocator: Allocator, key: K, value: V) Allocator.Error!void {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call putContext instead.");
            return self.putContext(allocator, key, value, undefined);
        }
        pub fn putContext(self: *Self, allocator: Allocator, key: K, value: V, ctx: Context) Allocator.Error!void {
            const result = try self.getOrPutContext(allocator, key, ctx);
            result.value_ptr.* = value;
        }

        /// Get an optional pointer to the actual key associated with adapted key, if present.
        pub fn getKeyPtr(self: Self, key: K) ?*K {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getKeyPtrContext instead.");
            return self.getKeyPtrContext(key, undefined);
        }
        pub fn getKeyPtrContext(self: Self, key: K, ctx: Context) ?*K {
            return self.getKeyPtrAdapted(key, ctx);
        }
        pub fn getKeyPtrAdapted(self: Self, key: anytype, ctx: anytype) ?*K {
            if (self.getIndex(key, ctx)) |idx| {
                return &self.keys()[idx];
            }
            return null;
        }

        /// Get a copy of the actual key associated with adapted key, if present.
        pub fn getKey(self: Self, key: K) ?K {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getKeyContext instead.");
            return self.getKeyContext(key, undefined);
        }
        pub fn getKeyContext(self: Self, key: K, ctx: Context) ?K {
            return self.getKeyAdapted(key, ctx);
        }
        pub fn getKeyAdapted(self: Self, key: anytype, ctx: anytype) ?K {
            if (self.getIndex(key, ctx)) |idx| {
                return self.keys()[idx];
            }
            return null;
        }

        /// Get an optional pointer to the value associated with key, if present.
        pub fn getPtr(self: Self, key: K) ?*V {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getPtrContext instead.");
            return self.getPtrContext(key, undefined);
        }
        pub fn getPtrContext(self: Self, key: K, ctx: Context) ?*V {
            return self.getPtrAdapted(key, ctx);
        }
        pub fn getPtrAdapted(self: Self, key: anytype, ctx: anytype) ?*V {
            if (self.getIndex(key, ctx)) |idx| {
                return &self.values()[idx];
            }
            return null;
        }

        /// Get a copy of the value associated with key, if present.
        pub fn get(self: Self, key: K) ?V {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getContext instead.");
            return self.getContext(key, undefined);
        }
        pub fn getContext(self: Self, key: K, ctx: Context) ?V {
            return self.getAdapted(key, ctx);
        }
        pub fn getAdapted(self: Self, key: anytype, ctx: anytype) ?V {
            if (self.getIndex(key, ctx)) |idx| {
                return self.values()[idx];
            }
            return null;
        }

        pub fn getOrPut(self: *Self, allocator: Allocator, key: K) Allocator.Error!GetOrPutResult {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getOrPutContext instead.");
            return self.getOrPutContext(allocator, key, undefined);
        }
        pub fn getOrPutContext(self: *Self, allocator: Allocator, key: K, ctx: Context) Allocator.Error!GetOrPutResult {
            const gop = try self.getOrPutContextAdapted(allocator, key, ctx, ctx);
            if (!gop.found_existing) {
                gop.key_ptr.* = key;
            }
            return gop;
        }
        pub fn getOrPutAdapted(self: *Self, allocator: Allocator, key: anytype, key_ctx: anytype) Allocator.Error!GetOrPutResult {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getOrPutContextAdapted instead.");
            return self.getOrPutContextAdapted(allocator, key, key_ctx, undefined);
        }
        pub fn getOrPutContextAdapted(self: *Self, allocator: Allocator, key: anytype, key_ctx: anytype, ctx: Context) Allocator.Error!GetOrPutResult {
            {
                self.pointer_stability.lock();
                defer self.pointer_stability.unlock();
                self.growIfNeeded(allocator, 1, ctx) catch |err| {
                    // If allocation fails, try to do the lookup anyway.
                    // If we find an existing item, we can return it.
                    // Otherwise return the error, we could not add another.
                    const index = self.getIndex(key, key_ctx) orelse return err;
                    return GetOrPutResult{
                        .key_ptr = &self.keys()[index],
                        .value_ptr = &self.values()[index],
                        .found_existing = true,
                    };
                };
            }
            return self.getOrPutAssumeCapacityAdapted(key, key_ctx);
        }

        pub fn getOrPutAssumeCapacity(self: *Self, key: K) GetOrPutResult {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getOrPutAssumeCapacityContext instead.");
            return self.getOrPutAssumeCapacityContext(key, undefined);
        }
        pub fn getOrPutAssumeCapacityContext(self: *Self, key: K, ctx: Context) GetOrPutResult {
            const result = self.getOrPutAssumeCapacityAdapted(key, ctx);
            if (!result.found_existing) {
                result.key_ptr.* = key;
            }
            return result;
        }
        pub fn getOrPutAssumeCapacityAdapted(self: *Self, key: anytype, ctx: anytype) GetOrPutResult {

            // If you get a compile error on this line, it means that your generic hash
            // function is invalid for these parameters.
            const hash: Hash = ctx.hash(key);

            const mask = self.capacity() - 1;
            const fingerprint = Metadata.takeFingerprint(hash);
            var limit = self.capacity();
            var idx = @as(usize, @truncate(hash & mask));

            var first_tombstone_idx: usize = self.capacity(); // invalid index
            var metadata = self.metadata.? + idx;
            while (!metadata[0].isFree() and limit != 0) {
                if (metadata[0].isUsed() and metadata[0].fingerprint == fingerprint) {
                    const test_key = &self.keys()[idx];
                    // If you get a compile error on this line, it means that your generic eql
                    // function is invalid for these parameters.

                    if (ctx.eql(key, test_key.*)) {
                        return GetOrPutResult{
                            .key_ptr = test_key,
                            .value_ptr = &self.values()[idx],
                            .found_existing = true,
                        };
                    }
                } else if (first_tombstone_idx == self.capacity() and metadata[0].isTombstone()) {
                    first_tombstone_idx = idx;
                }

                limit -= 1;
                idx = (idx + 1) & mask;
                metadata = self.metadata.? + idx;
            }

            if (first_tombstone_idx < self.capacity()) {
                // Cheap try to lower probing lengths after deletions. Recycle a tombstone.
                idx = first_tombstone_idx;
                metadata = self.metadata.? + idx;
            }
            // We're using a slot previously free or a tombstone.
            self.available -= 1;

            metadata[0].fill(fingerprint);
            const new_key = &self.keys()[idx];
            const new_value = &self.values()[idx];
            new_key.* = undefined;
            new_value.* = undefined;
            self.size += 1;

            return GetOrPutResult{
                .key_ptr = new_key,
                .value_ptr = new_value,
                .found_existing = false,
            };
        }

        pub fn getOrPutValue(self: *Self, allocator: Allocator, key: K, value: V) Allocator.Error!Entry {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call getOrPutValueContext instead.");
            return self.getOrPutValueContext(allocator, key, value, undefined);
        }
        pub fn getOrPutValueContext(self: *Self, allocator: Allocator, key: K, value: V, ctx: Context) Allocator.Error!Entry {
            const res = try self.getOrPutAdapted(allocator, key, ctx);
            if (!res.found_existing) {
                res.key_ptr.* = key;
                res.value_ptr.* = value;
            }
            return Entry{ .key_ptr = res.key_ptr, .value_ptr = res.value_ptr };
        }

        /// Return true if there is a value associated with key in the map.
        pub fn contains(self: Self, key: K) bool {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call containsContext instead.");
            return self.containsContext(key, undefined);
        }
        pub fn containsContext(self: Self, key: K, ctx: Context) bool {
            return self.containsAdapted(key, ctx);
        }
        pub fn containsAdapted(self: Self, key: anytype, ctx: anytype) bool {
            return self.getIndex(key, ctx) != null;
        }

        fn removeByIndex(self: *Self, idx: usize) void {
            self.metadata.?[idx].remove();
            self.keys()[idx] = undefined;
            self.values()[idx] = undefined;
            self.size -= 1;
            self.available += 1;
        }

        /// If there is an `Entry` with a matching key, it is deleted from
        /// the hash map, and this function returns true.  Otherwise this
        /// function returns false.
        ///
        /// TODO: answer the question in these doc comments, does this
        /// increase the unused capacity by one?
        pub fn remove(self: *Self, key: K) bool {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call removeContext instead.");
            return self.removeContext(key, undefined);
        }

        /// TODO: answer the question in these doc comments, does this
        /// increase the unused capacity by one?
        pub fn removeContext(self: *Self, key: K, ctx: Context) bool {
            return self.removeAdapted(key, ctx);
        }

        /// TODO: answer the question in these doc comments, does this
        /// increase the unused capacity by one?
        pub fn removeAdapted(self: *Self, key: anytype, ctx: anytype) bool {
            if (self.getIndex(key, ctx)) |idx| {
                self.removeByIndex(idx);
                return true;
            }

            return false;
        }

        /// Delete the entry with key pointed to by key_ptr from the hash map.
        /// key_ptr is assumed to be a valid pointer to a key that is present
        /// in the hash map.
        ///
        /// TODO: answer the question in these doc comments, does this
        /// increase the unused capacity by one?
        pub fn removeByPtr(self: *Self, key_ptr: *K) void {
            // if @sizeOf(K) == 0 then there is at most one item in the hash
            // map, which is assumed to exist as key_ptr must be valid.  This
            // item must be at index 0.
            const idx = if (@sizeOf(K) > 0)
                (key_ptr - self.keys())
            else
                0;

            self.removeByIndex(idx);
        }

        fn initMetadatas(self: *Self) void {
            @memset(@as([*]u8, @ptrCast(self.metadata.?))[0 .. @sizeOf(Metadata) * self.capacity()], 0);
        }

        // This counts the number of occupied slots (not counting tombstones), which is
        // what has to stay under the max_load_percentage of capacity.
        fn load(self: Self) Size {
            const max_load = (self.capacity() * max_load_percentage) / 100;
            assert(max_load >= self.available);
            return @as(Size, @truncate(max_load - self.available));
        }

        fn growIfNeeded(self: *Self, allocator: Allocator, new_count: Size, ctx: Context) Allocator.Error!void {
            if (new_count > self.available) {
                try self.grow(allocator, capacityForSize(self.load() + new_count), ctx);
            }
        }

        pub fn clone(self: Self, allocator: Allocator) Allocator.Error!Self {
            if (@sizeOf(Context) != 0)
                @compileError("Cannot infer context " ++ @typeName(Context) ++ ", call cloneContext instead.");
            return self.cloneContext(allocator, @as(Context, undefined));
        }
        pub fn cloneContext(self: Self, allocator: Allocator, new_ctx: anytype) Allocator.Error!HashMapUnmanaged(K, V, @TypeOf(new_ctx), max_load_percentage) {
            var other: HashMapUnmanaged(K, V, @TypeOf(new_ctx), max_load_percentage) = .empty;
            if (self.size == 0)
                return other;

            const new_cap = capacityForSize(self.size);
            try other.allocate(allocator, new_cap);
            other.initMetadatas();
            other.available = @truncate((new_cap * max_load_percentage) / 100);

            var i: Size = 0;
            var metadata = self.metadata.?;
            const keys_ptr = self.keys();
            const values_ptr = self.values();
            while (i < self.capacity()) : (i += 1) {
                if (metadata[i].isUsed()) {
                    other.putAssumeCapacityNoClobberContext(keys_ptr[i], values_ptr[i], new_ctx);
                    if (other.size == self.size)
                        break;
                }
            }

            return other;
        }

        /// Set the map to an empty state, making deinitialization a no-op, and
        /// returning a copy of the original.
        pub fn move(self: *Self) Self {
            self.pointer_stability.assertUnlocked();
            const result = self.*;
            self.* = .empty;
            return result;
        }

        /// Rehash the map, in-place.
        ///
        /// Over time, due to the current tombstone-based implementation, a
        /// HashMap could become fragmented due to the buildup of tombstone
        /// entries that causes a performance degradation due to excessive
        /// probing. The kind of pattern that might cause this is a long-lived
        /// HashMap with repeated inserts and deletes.
        ///
        /// After this function is called, there will be no tombstones in
        /// the HashMap, each of the entries is rehashed and any existing
        /// key/value pointers into the HashMap are invalidated.
        pub fn rehash(self: *Self, ctx: anytype) void {
            const mask = self.capacity() - 1;

            var metadata = self.metadata.?;
            var keys_ptr = self.keys();
            var values_ptr = self.values();
            var curr: Size = 0;

            // While we are re-hashing every slot, we will use the
            // fingerprint to mark used buckets as being used and either free
            // (needing to be rehashed) or tombstone (already rehashed).

            while (curr < self.capacity()) : (curr += 1) {
                metadata[curr].fingerprint = Metadata.free;
            }

            // Now iterate over all the buckets, rehashing them

            curr = 0;
            while (curr < self.capacity()) {
                if (!metadata[curr].isUsed()) {
                    assert(metadata[curr].isFree());
                    curr += 1;
                    continue;
                }

                const hash = ctx.hash(keys_ptr[curr]);
                const fingerprint = Metadata.takeFingerprint(hash);
                var idx = @as(usize, @truncate(hash & mask));

                // For each bucket, rehash to an index:
                // 1) before the cursor, probed into a free slot, or
                // 2) equal to the cursor, no need to move, or
                // 3) ahead of the cursor, probing over already rehashed

                while ((idx < curr and metadata[idx].isUsed()) or
                    (idx > curr and metadata[idx].fingerprint == Metadata.tombstone))
                {
                    idx = (idx + 1) & mask;
                }

                if (idx < curr) {
                    assert(metadata[idx].isFree());
                    metadata[idx].fill(fingerprint);
                    keys_ptr[idx] = keys_ptr[curr];
                    values_ptr[idx] = values_ptr[curr];

                    metadata[curr].used = 0;
                    assert(metadata[curr].isFree());
                    keys_ptr[curr] = undefined;
                    values_ptr[curr] = undefined;

                    curr += 1;
                } else if (idx == curr) {
                    metadata[idx].fingerprint = fingerprint;
                    curr += 1;
                } else {
                    assert(metadata[idx].fingerprint != Metadata.tombstone);
                    metadata[idx].fingerprint = Metadata.tombstone;
                    if (metadata[idx].isUsed()) {
                        std.mem.swap(K, &keys_ptr[curr], &keys_ptr[idx]);
                        std.mem.swap(V, &values_ptr[curr], &values_ptr[idx]);
                    } else {
                        metadata[idx].used = 1;
                        keys_ptr[idx] = keys_ptr[curr];
                        values_ptr[idx] = values_ptr[curr];

                        metadata[curr].fingerprint = Metadata.free;
                        metadata[curr].used = 0;
                        keys_ptr[curr] = undefined;
                        values_ptr[curr] = undefined;

                        curr += 1;
                    }
                }
            }
        }

        fn grow(self: *Self, allocator: Allocator, new_capacity: Size, ctx: Context) Allocator.Error!void {
            @branchHint(.cold);
            const new_cap = @max(new_capacity, minimal_capacity);
            assert(new_cap > self.capacity());
            assert(std.math.isPowerOfTwo(new_cap));

            var map: Self = .{};
            try map.allocate(allocator, new_cap);
            errdefer comptime unreachable;
            map.pointer_stability.lock();
            map.initMetadatas();
            map.available = @truncate((new_cap * max_load_percentage) / 100);

            if (self.size != 0) {
                const old_capacity = self.capacity();
                for (
                    self.metadata.?[0..old_capacity],
                    self.keys()[0..old_capacity],
                    self.values()[0..old_capacity],
                ) |m, k, v| {
                    if (!m.isUsed()) continue;
                    map.putAssumeCapacityNoClobberContext(k, v, ctx);
                    if (map.size == self.size) break;
                }
            }

            self.size = 0;
            self.pointer_stability = .{};
            std.mem.swap(Self, self, &map);
            map.deinit(allocator);
        }

        fn allocate(self: *Self, allocator: Allocator, new_capacity: Size) Allocator.Error!void {
            const header_align = @alignOf(Header);
            const key_align = if (@sizeOf(K) == 0) 1 else @alignOf(K);
            const val_align = if (@sizeOf(V) == 0) 1 else @alignOf(V);
            const max_align: Alignment = comptime .fromByteUnits(@max(header_align, key_align, val_align));

            const new_cap: usize = new_capacity;
            const meta_size = @sizeOf(Header) + new_cap * @sizeOf(Metadata);
            comptime assert(@alignOf(Metadata) == 1);

            const keys_start = std.mem.alignForward(usize, meta_size, key_align);
            const keys_end = keys_start + new_cap * @sizeOf(K);

            const vals_start = std.mem.alignForward(usize, keys_end, val_align);
            const vals_end = vals_start + new_cap * @sizeOf(V);

            const total_size = max_align.forward(vals_end);

            const slice = try allocator.alignedAlloc(u8, max_align, total_size);
            const ptr: [*]u8 = @ptrCast(slice.ptr);

            const metadata = ptr + @sizeOf(Header);

            const hdr = @as(*Header, @ptrCast(@alignCast(ptr)));
            if (@sizeOf([*]V) != 0) {
                hdr.values = @ptrCast(@alignCast((ptr + vals_start)));
            }
            if (@sizeOf([*]K) != 0) {
                hdr.keys = @ptrCast(@alignCast((ptr + keys_start)));
            }
            hdr.capacity = new_capacity;
            self.metadata = @ptrCast(@alignCast(metadata));
        }

        fn deallocate(self: *Self, allocator: Allocator) void {
            if (self.metadata == null) return;

            const header_align = @alignOf(Header);
            const key_align = if (@sizeOf(K) == 0) 1 else @alignOf(K);
            const val_align = if (@sizeOf(V) == 0) 1 else @alignOf(V);
            const max_align = comptime @max(header_align, key_align, val_align);

            const cap: usize = self.capacity();
            const meta_size = @sizeOf(Header) + cap * @sizeOf(Metadata);
            comptime assert(@alignOf(Metadata) == 1);

            const keys_start = std.mem.alignForward(usize, meta_size, key_align);
            const keys_end = keys_start + cap * @sizeOf(K);

            const vals_start = std.mem.alignForward(usize, keys_end, val_align);
            const vals_end = vals_start + cap * @sizeOf(V);

            const total_size = std.mem.alignForward(usize, vals_end, max_align);

            const slice = @as([*]align(max_align) u8, @ptrCast(@alignCast(self.header())))[0..total_size];
            allocator.free(slice);

            self.metadata = null;
            self.available = 0;
        }

        /// This function is used in the debugger pretty formatters in tools/ to fetch the
        /// header type to facilitate fancy debug printing for this type.
        fn dbHelper(self: *Self, hdr: *Header, entry: *Entry) void {
            _ = self;
            _ = hdr;
            _ = entry;
        }

        comptime {
            if (!builtin.strip_debug_info) switch (builtin.zig_backend) {
                .stage2_llvm => _ = &dbHelper,
                .stage2_x86_64 => _ = @as(KV, undefined),
                else => {},
            };
        }
    };
}

const testing = std.testing;
const expect = std.testing.expect;
const expectEqual = std.testing.expectEqual;

test "basic usage" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    const count = 5;
    var i: u32 = 0;
    var total: u32 = 0;
    while (i < count) : (i += 1) {
        try map.put(i, i);
        total += i;
    }

    var sum: u32 = 0;
    var it = map.iterator();
    while (it.next()) |kv| {
        sum += kv.key_ptr.*;
    }
    try expectEqual(total, sum);

    i = 0;
    sum = 0;
    while (i < count) : (i += 1) {
        try expectEqual(i, map.get(i).?);
        sum += map.get(i).?;
    }
    try expectEqual(total, sum);
}

test "ensureTotalCapacity" {
    var map = AutoHashMap(i32, i32).init(std.testing.allocator);
    defer map.deinit();

    try map.ensureTotalCapacity(20);
    const initial_capacity = map.capacity();
    try testing.expect(initial_capacity >= 20);
    var i: i32 = 0;
    while (i < 20) : (i += 1) {
        try testing.expect(map.fetchPutAssumeCapacity(i, i + 10) == null);
    }
    // shouldn't resize from putAssumeCapacity
    try testing.expect(initial_capacity == map.capacity());
}

test "ensureUnusedCapacity with tombstones" {
    var map = AutoHashMap(i32, i32).init(std.testing.allocator);
    defer map.deinit();

    var i: i32 = 0;
    while (i < 100) : (i += 1) {
        try map.ensureUnusedCapacity(1);
        map.putAssumeCapacity(i, i);
        _ = map.remove(i);
    }
}

test "clearRetainingCapacity" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    map.clearRetainingCapacity();

    try map.put(1, 1);
    try expectEqual(map.get(1).?, 1);
    try expectEqual(map.count(), 1);

    map.clearRetainingCapacity();
    map.putAssumeCapacity(1, 1);
    try expectEqual(map.get(1).?, 1);
    try expectEqual(map.count(), 1);

    const cap = map.capacity();
    try expect(cap > 0);

    map.clearRetainingCapacity();
    map.clearRetainingCapacity();
    try expectEqual(map.count(), 0);
    try expectEqual(map.capacity(), cap);
    try expect(!map.contains(1));
}

test "grow" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    const growTo = 12456;

    var i: u32 = 0;
    while (i < growTo) : (i += 1) {
        try map.put(i, i);
    }
    try expectEqual(map.count(), growTo);

    i = 0;
    var it = map.iterator();
    while (it.next()) |kv| {
        try expectEqual(kv.key_ptr.*, kv.value_ptr.*);
        i += 1;
    }
    try expectEqual(i, growTo);

    i = 0;
    while (i < growTo) : (i += 1) {
        try expectEqual(map.get(i).?, i);
    }
}

test "clone" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    var a = try map.clone();
    defer a.deinit();

    try expectEqual(a.count(), 0);

    try a.put(1, 1);
    try a.put(2, 2);
    try a.put(3, 3);

    var b = try a.clone();
    defer b.deinit();

    try expectEqual(b.count(), 3);
    try expectEqual(b.get(1).?, 1);
    try expectEqual(b.get(2).?, 2);
    try expectEqual(b.get(3).?, 3);

    var original = AutoHashMap(i32, i32).init(std.testing.allocator);
    defer original.deinit();

    var i: u8 = 0;
    while (i < 10) : (i += 1) {
        try original.putNoClobber(i, i * 10);
    }

    var copy = try original.clone();
    defer copy.deinit();

    i = 0;
    while (i < 10) : (i += 1) {
        try testing.expect(copy.get(i).? == i * 10);
    }
}

test "ensureTotalCapacity with existing elements" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    try map.put(0, 0);
    try expectEqual(map.count(), 1);
    try expectEqual(map.capacity(), @TypeOf(map).Unmanaged.minimal_capacity);

    try map.ensureTotalCapacity(65);
    try expectEqual(map.count(), 1);
    try expectEqual(map.capacity(), 128);
}

test "ensureTotalCapacity satisfies max load factor" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    try map.ensureTotalCapacity(127);
    try expectEqual(map.capacity(), 256);
}

test "remove" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    var i: u32 = 0;
    while (i < 16) : (i += 1) {
        try map.put(i, i);
    }

    i = 0;
    while (i < 16) : (i += 1) {
        if (i % 3 == 0) {
            _ = map.remove(i);
        }
    }
    try expectEqual(map.count(), 10);
    var it = map.iterator();
    while (it.next()) |kv| {
        try expectEqual(kv.key_ptr.*, kv.value_ptr.*);
        try expect(kv.key_ptr.* % 3 != 0);
    }

    i = 0;
    while (i < 16) : (i += 1) {
        if (i % 3 == 0) {
            try expect(!map.contains(i));
        } else {
            try expectEqual(map.get(i).?, i);
        }
    }
}

test "reverse removes" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    var i: u32 = 0;
    while (i < 16) : (i += 1) {
        try map.putNoClobber(i, i);
    }

    i = 16;
    while (i > 0) : (i -= 1) {
        _ = map.remove(i - 1);
        try expect(!map.contains(i - 1));
        var j: u32 = 0;
        while (j < i - 1) : (j += 1) {
            try expectEqual(map.get(j).?, j);
        }
    }

    try expectEqual(map.count(), 0);
}

test "multiple removes on same metadata" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    var i: u32 = 0;
    while (i < 16) : (i += 1) {
        try map.put(i, i);
    }

    _ = map.remove(7);
    _ = map.remove(15);
    _ = map.remove(14);
    _ = map.remove(13);
    try expect(!map.contains(7));
    try expect(!map.contains(15));
    try expect(!map.contains(14));
    try expect(!map.contains(13));

    i = 0;
    while (i < 13) : (i += 1) {
        if (i == 7) {
            try expect(!map.contains(i));
        } else {
            try expectEqual(map.get(i).?, i);
        }
    }

    try map.put(15, 15);
    try map.put(13, 13);
    try map.put(14, 14);
    try map.put(7, 7);
    i = 0;
    while (i < 16) : (i += 1) {
        try expectEqual(map.get(i).?, i);
    }
}

test "put and remove loop in random order" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    var keys = std.array_list.Managed(u32).init(std.testing.allocator);
    defer keys.deinit();

    const size = 32;
    const iterations = 100;

    var i: u32 = 0;
    while (i < size) : (i += 1) {
        try keys.append(i);
    }
    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    while (i < iterations) : (i += 1) {
        random.shuffle(u32, keys.items);

        for (keys.items) |key| {
            try map.put(key, key);
        }
        try expectEqual(map.count(), size);

        for (keys.items) |key| {
            _ = map.remove(key);
        }
        try expectEqual(map.count(), 0);
    }
}

test "remove many elements in random order" {
    const Map = AutoHashMap(u32, u32);
    const n = 1000 * 100;
    var map = Map.init(std.heap.page_allocator);
    defer map.deinit();

    var keys = std.array_list.Managed(u32).init(std.heap.page_allocator);
    defer keys.deinit();

    var i: u32 = 0;
    while (i < n) : (i += 1) {
        keys.append(i) catch unreachable;
    }

    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();
    random.shuffle(u32, keys.items);

    for (keys.items) |key| {
        map.put(key, key) catch unreachable;
    }

    random.shuffle(u32, keys.items);
    i = 0;
    while (i < n) : (i += 1) {
        const key = keys.items[i];
        _ = map.remove(key);
    }
}

test "put" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    var i: u32 = 0;
    while (i < 16) : (i += 1) {
        try map.put(i, i);
    }

    i = 0;
    while (i < 16) : (i += 1) {
        try expectEqual(map.get(i).?, i);
    }

    i = 0;
    while (i < 16) : (i += 1) {
        try map.put(i, i * 16 + 1);
    }

    i = 0;
    while (i < 16) : (i += 1) {
        try expectEqual(map.get(i).?, i * 16 + 1);
    }
}

test "putAssumeCapacity" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    try map.ensureTotalCapacity(20);
    var i: u32 = 0;
    while (i < 20) : (i += 1) {
        map.putAssumeCapacityNoClobber(i, i);
    }

    i = 0;
    var sum = i;
    while (i < 20) : (i += 1) {
        sum += map.getPtr(i).?.*;
    }
    try expectEqual(sum, 190);

    i = 0;
    while (i < 20) : (i += 1) {
        map.putAssumeCapacity(i, 1);
    }

    i = 0;
    sum = i;
    while (i < 20) : (i += 1) {
        sum += map.get(i).?;
    }
    try expectEqual(sum, 20);
}

test "repeat putAssumeCapacity/remove" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    try map.ensureTotalCapacity(20);
    const limit = map.unmanaged.available;

    var i: u32 = 0;
    while (i < limit) : (i += 1) {
        map.putAssumeCapacityNoClobber(i, i);
    }

    // Repeatedly delete/insert an entry without resizing the map.
    // Put to different keys so entries don't land in the just-freed slot.
    i = 0;
    while (i < 10 * limit) : (i += 1) {
        try testing.expect(map.remove(i));
        if (i % 2 == 0) {
            map.putAssumeCapacityNoClobber(limit + i, i);
        } else {
            map.putAssumeCapacity(limit + i, i);
        }
    }

    i = 9 * limit;
    while (i < 10 * limit) : (i += 1) {
        try expectEqual(map.get(limit + i), i);
    }
    try expectEqual(map.unmanaged.available, 0);
    try expectEqual(map.unmanaged.count(), limit);
}

test "getOrPut" {
    var map = AutoHashMap(u32, u32).init(std.testing.allocator);
    defer map.deinit();

    var i: u32 = 0;
    while (i < 10) : (i += 1) {
        try map.put(i * 2, 2);
    }

    i = 0;
    while (i < 20) : (i += 1) {
        _ = try map.getOrPutValue(i, 1);
    }

    i = 0;
    var sum = i;
    while (i < 20) : (i += 1) {
        sum += map.get(i).?;
    }

    try expectEqual(sum, 30);
}

test "basic hash map usage" {
    var map = AutoHashMap(i32, i32).init(std.testing.allocator);
    defer map.deinit();

    try testing.expect((try map.fetchPut(1, 11)) == null);
    try testing.expect((try map.fetchPut(2, 22)) == null);
    try testing.expect((try map.fetchPut(3, 33)) == null);
    try testing.expect((try map.fetchPut(4, 44)) == null);

    try map.putNoClobber(5, 55);
    try testing.expect((try map.fetchPut(5, 66)).?.value == 55);
    try testing.expect((try map.fetchPut(5, 55)).?.value == 66);

    const gop1 = try map.getOrPut(5);
    try testing.expect(gop1.found_existing == true);
    try testing.expect(gop1.value_ptr.* == 55);
    gop1.value_ptr.* = 77;
    try testing.expect(map.getEntry(5).?.value_ptr.* == 77);

    const gop2 = try map.getOrPut(99);
    try testing.expect(gop2.found_existing == false);
    gop2.value_ptr.* = 42;
    try testing.expect(map.getEntry(99).?.value_ptr.* == 42);

    const gop3 = try map.getOrPutValue(5, 5);
    try testing.expect(gop3.value_ptr.* == 77);

    const gop4 = try map.getOrPutValue(100, 41);
    try testing.expect(gop4.value_ptr.* == 41);

    try testing.expect(map.contains(2));
    try testing.expect(map.getEntry(2).?.value_ptr.* == 22);
    try testing.expect(map.get(2).? == 22);

    const rmv1 = map.fetchRemove(2);
    try testing.expect(rmv1.?.key == 2);
    try testing.expect(rmv1.?.value == 22);
    try testing.expect(map.fetchRemove(2) == null);
    try testing.expect(map.remove(2) == false);
    try testing.expect(map.getEntry(2) == null);
    try testing.expect(map.get(2) == null);

    try testing.expect(map.remove(3) == true);
}

test "getOrPutAdapted" {
    const AdaptedContext = struct {
        fn eql(self: @This(), adapted_key: []const u8, test_key: u64) bool {
            _ = self;
            return std.fmt.parseInt(u64, adapted_key, 10) catch unreachable == test_key;
        }
        fn hash(self: @This(), adapted_key: []const u8) u64 {
            _ = self;
            const key = std.fmt.parseInt(u64, adapted_key, 10) catch unreachable;
            return (AutoContext(u64){}).hash(key);
        }
    };
    var map = AutoHashMap(u64, u64).init(testing.allocator);
    defer map.deinit();

    const keys = [_][]const u8{
        "1231",
        "4564",
        "7894",
        "1132",
        "65235",
        "95462",
        "0112305",
        "00658",
        "0",
        "2",
    };

    var real_keys: [keys.len]u64 = undefined;

    inline for (keys, 0..) |key_str, i| {
        const result = try map.getOrPutAdapted(key_str, AdaptedContext{});
        try testing.expect(!result.found_existing);
        real_keys[i] = std.fmt.parseInt(u64, key_str, 10) catch unreachable;
        result.key_ptr.* = real_keys[i];
        result.value_ptr.* = i * 2;
    }

    try testing.expectEqual(map.count(), keys.len);

    inline for (keys, 0..) |key_str, i| {
        const result = map.getOrPutAssumeCapacityAdapted(key_str, AdaptedContext{});
        try testing.expect(result.found_existing);
        try testing.expectEqual(real_keys[i], result.key_ptr.*);
        try testing.expectEqual(@as(u64, i) * 2, result.value_ptr.*);
        try testing.expectEqual(real_keys[i], map.getKeyAdapted(key_str, AdaptedContext{}).?);
    }
}

test "ensureUnusedCapacity" {
    var map = AutoHashMap(u64, u64).init(testing.allocator);
    defer map.deinit();

    try map.ensureUnusedCapacity(32);
    const capacity = map.capacity();
    try map.ensureUnusedCapacity(32);

    // Repeated ensureUnusedCapacity() calls with no insertions between
    // should not change the capacity.
    try testing.expectEqual(capacity, map.capacity());
}

test "removeByPtr" {
    var map = AutoHashMap(i32, u64).init(testing.allocator);
    defer map.deinit();

    var i: i32 = undefined;

    i = 0;
    while (i < 10) : (i += 1) {
        try map.put(i, 0);
    }

    try testing.expect(map.count() == 10);

    i = 0;
    while (i < 10) : (i += 1) {
        const key_ptr = map.getKeyPtr(i);
        try testing.expect(key_ptr != null);

        if (key_ptr) |ptr| {
            map.removeByPtr(ptr);
        }
    }

    try testing.expect(map.count() == 0);
}

test "removeByPtr 0 sized key" {
    var map = AutoHashMap(u0, u64).init(testing.allocator);
    defer map.deinit();

    try map.put(0, 0);

    try testing.expect(map.count() == 1);

    const key_ptr = map.getKeyPtr(0);
    try testing.expect(key_ptr != null);

    if (key_ptr) |ptr| {
        map.removeByPtr(ptr);
    }

    try testing.expect(map.count() == 0);
}

test "repeat fetchRemove" {
    var map: AutoHashMapUnmanaged(u64, void) = .empty;
    defer map.deinit(testing.allocator);

    try map.ensureTotalCapacity(testing.allocator, 4);

    map.putAssumeCapacity(0, {});
    map.putAssumeCapacity(1, {});
    map.putAssumeCapacity(2, {});
    map.putAssumeCapacity(3, {});

    // fetchRemove() should make slots available.
    var i: usize = 0;
    while (i < 10) : (i += 1) {
        try testing.expect(map.fetchRemove(3) != null);
        map.putAssumeCapacity(3, {});
    }

    try testing.expect(map.get(0) != null);
    try testing.expect(map.get(1) != null);
    try testing.expect(map.get(2) != null);
    try testing.expect(map.get(3) != null);
}

test "getOrPut allocation failure" {
    var map: std.StringHashMapUnmanaged(void) = .empty;
    try testing.expectError(error.OutOfMemory, map.getOrPut(std.testing.failing_allocator, "hello"));
}

test "rehash" {
    var map = AutoHashMap(usize, usize).init(std.testing.allocator);
    defer map.deinit();

    var prng = std.Random.DefaultPrng.init(0);
    const random = prng.random();

    const count = 4 * random.intRangeLessThan(u32, 100_000, 500_000);

    for (0..count) |i| {
        try map.put(i, i);
        if (i % 3 == 0) {
            try expectEqual(map.remove(i), true);
        }
    }

    map.rehash();

    try expectEqual(map.count(), count * 2 / 3);

    for (0..count) |i| {
        if (i % 3 == 0) {
            try expectEqual(map.get(i), null);
        } else {
            try expectEqual(map.get(i).?, i);
        }
    }
}



---
File: /std/hash.zig
---

pub const Adler32 = @import("hash/Adler32.zig");

const auto_hash = @import("hash/auto_hash.zig");
pub const autoHash = auto_hash.autoHash;
pub const autoHashStrat = auto_hash.hash;
pub const Strategy = auto_hash.HashStrategy;

// pub for polynomials + generic crc32 construction
pub const crc = @import("hash/crc.zig");
pub const Crc32 = crc.Crc32;

const fnv = @import("hash/fnv.zig");
pub const Fnv1a_32 = fnv.Fnv1a_32;
pub const Fnv1a_64 = fnv.Fnv1a_64;
pub const Fnv1a_128 = fnv.Fnv1a_128;

const siphash = @import("crypto/siphash.zig");
pub const SipHash64 = siphash.SipHash64;
pub const SipHash128 = siphash.SipHash128;

pub const murmur = @import("hash/murmur.zig");
pub const Murmur2_32 = murmur.Murmur2_32;

pub const Murmur2_64 = murmur.Murmur2_64;
pub const Murmur3_32 = murmur.Murmur3_32;

pub const cityhash = @import("hash/cityhash.zig");
pub const CityHash32 = cityhash.CityHash32;
pub const CityHash64 = cityhash.CityHash64;

const wyhash = @import("hash/wyhash.zig");
pub const Wyhash = wyhash.Wyhash;

const xxhash = @import("hash/xxhash.zig");
pub const XxHash3 = xxhash.XxHash3;
pub const XxHash64 = xxhash.XxHash64;
pub const XxHash32 = xxhash.XxHash32;

/// Integer-to-integer hashing for bit widths <= 256.
pub fn int(input: anytype) @TypeOf(input) {
    // This function is only intended for integer types
    const info = @typeInfo(@TypeOf(input)).int;
    const bits = info.bits;
    // Convert input to unsigned integer (easier to deal with)
    const Uint = @Int(.unsigned, bits);
    const u_input: Uint = @bitCast(input);
    if (bits > 256) @compileError("bit widths > 256 are unsupported, use std.hash.autoHash functionality.");
    // For bit widths that don't have a dedicated function, use a heuristic
    // construction with a multiplier suited to diffusion -
    // a mod 2^bits where a^2 - 46 * a + 1 = 0 mod 2^(bits + 4),
    // on Mathematica: bits = 256; BaseForm[Solve[1 - 46 a + a^2 == 0, a, Modulus -> 2^(bits + 4)][[-1]][[1]][[2]], 16]
    const mult: Uint = @truncate(0xfac2e27ed2036860a062b5f264d80a512b00aa459b448bf1eca24d41c96f59e5b);
    // The bit width of the input integer determines how to hash it
    const output = switch (bits) {
        0...2 => u_input *% mult,
        16 => uint16(u_input),
        32 => uint32(u_input),
        64 => uint64(u_input),
        else => blk: {
            var x: Uint = u_input;
            inline for (0..4) |_| {
                x ^= x >> (bits / 2);
                x *%= mult;
            }
            break :blk x;
        },
    };
    return @bitCast(output);
}

/// Source: https://github.com/skeeto/hash-prospector
fn uint16(input: u16) u16 {
    var x: u16 = input;
    x = (x ^ (x >> 7)) *% 0x2993;
    x = (x ^ (x >> 5)) *% 0xe877;
    x = (x ^ (x >> 9)) *% 0x0235;
    x = x ^ (x >> 10);
    return x;
}

/// Source: https://github.com/skeeto/hash-prospector
fn uint32(input: u32) u32 {
    var x: u32 = input;
    x = (x ^ (x >> 17)) *% 0xed5ad4bb;
    x = (x ^ (x >> 11)) *% 0xac4c1b51;
    x = (x ^ (x >> 15)) *% 0x31848bab;
    x = x ^ (x >> 14);
    return x;
}

/// Source: https://github.com/jonmaiga/mx3
fn uint64(input: u64) u64 {
    var x: u64 = input;
    const c = 0xbea225f9eb34556d;
    x = (x ^ (x >> 32)) *% c;
    x = (x ^ (x >> 29)) *% c;
    x = (x ^ (x >> 32)) *% c;
    x = x ^ (x >> 29);
    return x;
}

test int {
    const expectEqual = @import("std").testing.expectEqual;
    try expectEqual(0x1, int(@as(u1, 1)));
    try expectEqual(0x3, int(@as(u2, 1)));
    try expectEqual(0x4, int(@as(u3, 1)));
    try expectEqual(0xD6, int(@as(u8, 1)));
    try expectEqual(0x2880, int(@as(u16, 1)));
    try expectEqual(0x2880, int(@as(i16, 1)));
    try expectEqual(0x838380, int(@as(u24, 1)));
    try expectEqual(0x42741D6, int(@as(u32, 1)));
    try expectEqual(0x42741D6, int(@as(i32, 1)));
    try expectEqual(0x71894DE00D9981F, int(@as(u64, 1)));
    try expectEqual(0x71894DE00D9981F, int(@as(i64, 1)));
}

test {
    _ = Adler32;
    _ = auto_hash;
    _ = crc;
    _ = fnv;
    _ = murmur;
    _ = cityhash;
    _ = wyhash;
    _ = xxhash;
}



---
File: /std/heap.zig
---

const std = @import("std.zig");
const builtin = @import("builtin");
const root = @import("root");
const assert = std.debug.assert;
const testing = std.testing;
const mem = std.mem;
const c = std.c;
const Allocator = std.mem.Allocator;
const windows = std.os.windows;
const Alignment = std.mem.Alignment;

pub const ArenaAllocator = @import("heap/ArenaAllocator.zig");
pub const SmpAllocator = @import("heap/SmpAllocator.zig");
pub const FixedBufferAllocator = @import("heap/FixedBufferAllocator.zig");
pub const PageAllocator = @import("heap/PageAllocator.zig");
pub const WasmAllocator = if (builtin.single_threaded) BrkAllocator else @compileError("unimplemented");
pub const BrkAllocator = @import("heap/BrkAllocator.zig");

pub const DebugAllocatorConfig = @import("heap/debug_allocator.zig").Config;
pub const DebugAllocator = @import("heap/debug_allocator.zig").DebugAllocator;
pub const Check = enum { ok, leak };

/// A memory pool that can allocate objects of a single type very quickly.
/// Use this when you need to allocate a lot of objects of the same type,
/// because it outperforms general purpose allocators.
/// Functions that potentially allocate memory accept an `Allocator` parameter.
pub fn MemoryPool(comptime Item: type) type {
    return memory_pool.Extra(Item, .{ .alignment = null });
}
pub const memory_pool = @import("heap/memory_pool.zig");

/// Deprecated; use `memory_pool.Aligned`.
pub const MemoryPoolAligned = memory_pool.Aligned;
/// Deprecated; use `memory_pool.Extra`.
pub const MemoryPoolExtra = memory_pool.Extra;
/// Deprecated; use `memory_pool.Options`.
pub const MemoryPoolOptions = memory_pool.Options;

/// comptime-known minimum page size of the target.
///
/// All pointers from `mmap` or `NtAllocateVirtualMemory` are aligned to at least
/// `page_size_min`, but their actual alignment may be bigger.
///
/// This value can be overridden via `std.options.page_size_min`.
///
/// On many systems, the actual page size can only be determined at runtime
/// with `pageSize`.
pub const page_size_min: usize = std.options.page_size_min orelse (page_size_min_default orelse @compileError(@tagName(builtin.cpu.arch) ++ "-" ++ @tagName(builtin.os.tag) ++ " has unknown page_size_min; populate std.options.page_size_min"));
/// comptime-known maximum page size of the target.
///
/// Targeting a system with a larger page size may require overriding
/// `std.options.page_size_max`, as well as providing a corresponding linker
/// option.
///
/// The actual page size can only be determined at runtime with `pageSize`.
pub const page_size_max: usize = std.options.page_size_max orelse (page_size_max_default orelse if (builtin.os.tag == .freestanding or builtin.os.tag == .other)
    @compileError("freestanding/other page_size_max must provided with std.options.page_size_max")
else
    @compileError(@tagName(builtin.cpu.arch) ++ "-" ++ @tagName(builtin.os.tag) ++ " has unknown page_size_max; populate std.options.page_size_max"));

/// If the page size is comptime-known, return value is comptime.
/// Otherwise, calls `std.options.queryPageSize` which by default queries the
/// host operating system at runtime.
pub inline fn pageSize() usize {
    if (page_size_min == page_size_max) return page_size_min;
    return std.options.queryPageSize();
}

test pageSize {
    assert(std.math.isPowerOfTwo(pageSize()));
}

/// The default implementation of `std.options.queryPageSize`.
/// Asserts that the page size is within `page_size_min` and `page_size_max`
pub fn defaultQueryPageSize() usize {
    const global = struct {
        var cached_result: std.atomic.Value(usize) = .init(0);
    };
    var size = global.cached_result.load(.unordered);
    if (size > 0) return size;
    size = size: switch (builtin.os.tag) {
        .linux => if (builtin.link_libc)
            @max(std.c.sysconf(@intFromEnum(std.c._SC.PAGESIZE)), 0)
        else
            std.os.linux.getauxval(std.elf.AT_PAGESZ),
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => {
            const task_port = std.c.mach_task_self();
            // mach_task_self may fail "if there are any resource failures or other errors".
            if (task_port == std.c.TASK.NULL) break :size 0;
            var info_count = std.c.TASK.VM.INFO_COUNT;
            var vm_info: std.c.task_vm_info_data_t = undefined;
            vm_info.page_size = 0;
            _ = std.c.task_info(
                task_port,
                std.c.TASK.VM.INFO,
                @as(std.c.task_info_t, @ptrCast(&vm_info)),
                &info_count,
            );
            break :size @intCast(vm_info.page_size);
        },
        .windows => {
            var sbi: windows.SYSTEM.BASIC_INFORMATION = undefined;
            switch (windows.ntdll.NtQuerySystemInformation(
                .Basic,
                &sbi,
                @sizeOf(windows.SYSTEM.BASIC_INFORMATION),
                null,
            )) {
                .SUCCESS => break :size sbi.PageSize,
                else => break :size 0,
            }
        },
        else => if (builtin.link_libc)
            @max(std.c.sysconf(@intFromEnum(std.c._SC.PAGESIZE)), 0)
        else if (builtin.os.tag == .freestanding or builtin.os.tag == .other)
            @compileError("unsupported target: freestanding/other")
        else
            @compileError("pageSize on " ++ @tagName(builtin.cpu.arch) ++ "-" ++ @tagName(builtin.os.tag) ++ " is not supported without linking libc, using the default implementation"),
    };
    if (size == 0) size = page_size_max;

    assert(size >= page_size_min);
    assert(size <= page_size_max);
    global.cached_result.store(size, .unordered);

    return size;
}

test defaultQueryPageSize {
    if (builtin.cpu.arch.isWasm()) return error.SkipZigTest;
    assert(std.math.isPowerOfTwo(defaultQueryPageSize()));
}

/// A wrapper around the C memory allocation API which supports the full `Allocator`
/// interface, including arbitrary alignment. Simple `malloc` calls are used when
/// possible, but large requested alignments may require larger buffers in order to
/// satisfy the request. As well as `malloc`, `realloc`, and `free`, the extension
/// functions `malloc_usable_size` and `posix_memalign` are used when available.
pub const c_allocator: Allocator = .{
    .ptr = undefined,
    .vtable = &c_allocator_impl.vtable,
};
const c_allocator_impl = struct {
    comptime {
        if (!builtin.link_libc) {
            @compileError("C allocator is only available when linking against libc");
        }
    }

    const vtable: Allocator.VTable = .{
        .alloc = alloc,
        .resize = resize,
        .remap = remap,
        .free = free,
    };

    const have_posix_memalign = switch (builtin.os.tag) {
        .dragonfly,
        .netbsd,
        .freebsd,
        .illumos,
        .openbsd,
        .linux,
        .driverkit,
        .ios,
        .maccatalyst,
        .macos,
        .tvos,
        .visionos,
        .watchos,
        .serenity,
        => true,
        else => false,
    };

    fn allocStrat(need_align: Alignment) union(enum) {
        raw,
        posix_memalign: if (have_posix_memalign) void else noreturn,
        manual_align: if (have_posix_memalign) noreturn else void,
    } {
        // If `malloc` guarantees `need_align`, always prefer a raw allocation.
        if (Alignment.compare(need_align, .lte, .of(c.max_align_t))) {
            return .raw;
        }
        // Use `posix_memalign` if available. Otherwise, we must manually align the allocation.
        return if (have_posix_memalign) .posix_memalign else .manual_align;
    }

    /// If `allocStrat(a) == .manual_align`, an allocation looks like this:
    ///
    /// unaligned_ptr   hdr_ptr  aligned_ptr
    /// v               v        v
    /// +---------------+--------+--------------+
    /// |    padding    | header | usable bytes |
    /// +---------------+--------+--------------+
    ///
    /// * `unaligned_ptr` is the raw return value of `malloc`.
    /// * `aligned_ptr` is computed by aligning `unaligned_ptr` forward; it is what `alloc` returns.
    /// * `hdr_ptr` points to a pointer-sized header directly before the usable space. This header
    ///   contains the value `unaligned_ptr`, so that we can pass it to `free` later. This is
    ///   necessary because the width of the padding is unknown.
    ///
    /// This function accepts `aligned_ptr` and offsets it backwards to return `hdr_ptr`.
    fn manualAlignHeader(aligned_ptr: [*]u8) *[*]u8 {
        return @ptrCast(@alignCast(aligned_ptr - @sizeOf(usize)));
    }

    fn alloc(
        _: *anyopaque,
        len: usize,
        alignment: Alignment,
        return_address: usize,
    ) ?[*]u8 {
        _ = return_address;
        assert(len > 0);
        switch (allocStrat(alignment)) {
            .raw => {
                // `std.c.max_align_t` isn't the whole story, because if `len` is smaller than
                // every C type with alignment `max_align_t`, the allocation can be less-aligned.
                // The implementation need only guarantee that any type of length `len` would be
                // suitably aligned.
                //
                // For instance, if `len == 8` and `alignment == .@"16"`, then `malloc` may not
                // fulfil this request, because there is necessarily no C type with 8-byte size
                // but 16-byte alignment.
                //
                // In theory, the resulting rule here would be target-specific, but in practice,
                // the smallest type with an alignment of `max_align_t` has the same size (it's
                // usually `c_longdouble`), so we can just extend the allocation size up to the
                // alignment of `max_align_t` if necessary.
                const actual_len = @max(len, @alignOf(std.c.max_align_t));
                const ptr = c.malloc(actual_len) orelse return null;
                assert(alignment.check(@intFromPtr(ptr)));
                return @ptrCast(ptr);
            },
            .posix_memalign => {
                // The posix_memalign only accepts alignment values that are a
                // multiple of the pointer size
                const effective_alignment = @max(alignment.toByteUnits(), @sizeOf(usize));
                var aligned_ptr: ?*anyopaque = undefined;
                if (c.posix_memalign(&aligned_ptr, effective_alignment, len) != 0) {
                    return null;
                }
                assert(alignment.check(@intFromPtr(aligned_ptr)));
                return @ptrCast(aligned_ptr);
            },
            .manual_align => {
                // Overallocate to account for alignment padding and store the original pointer
                // returned by `malloc` before the aligned address.
                const padded_len = len + @sizeOf(usize) + alignment.toByteUnits() - 1;
                const unaligned_ptr: [*]u8 = @ptrCast(c.malloc(padded_len) orelse return null);
                const unaligned_addr = @intFromPtr(unaligned_ptr);
                const aligned_addr = alignment.forward(unaligned_addr + @sizeOf(usize));
                const aligned_ptr = unaligned_ptr + (aligned_addr - unaligned_addr);
                manualAlignHeader(aligned_ptr).* = unaligned_ptr;
                return aligned_ptr;
            },
        }
    }

    fn resize(
        _: *anyopaque,
        memory: []u8,
        alignment: Alignment,
        new_len: usize,
        return_address: usize,
    ) bool {
        _ = return_address;
        assert(new_len > 0);
        if (new_len <= memory.len) {
            return true; // in-place shrink always works
        }
        const mallocSize = func: {
            if (@TypeOf(c.malloc_size) != void) break :func c.malloc_size;
            if (@TypeOf(c.malloc_usable_size) != void) break :func c.malloc_usable_size;
            if (@TypeOf(c._msize) != void) break :func c._msize;
            return false; // we don't know how much space is actually available
        };
        const usable_len: usize = switch (allocStrat(alignment)) {
            .raw, .posix_memalign => mallocSize(memory.ptr),
            .manual_align => usable_len: {
                const unaligned_ptr = manualAlignHeader(memory.ptr).*;
                const full_len = mallocSize(unaligned_ptr);
                const padding = @intFromPtr(memory.ptr) - @intFromPtr(unaligned_ptr);
                break :usable_len full_len - padding;
            },
        };
        return new_len <= usable_len;
    }

    fn remap(
        ctx: *anyopaque,
        memory: []u8,
        alignment: Alignment,
        new_len: usize,
        return_address: usize,
    ) ?[*]u8 {
        assert(new_len > 0);
        // Prefer resizing in-place if possible, since `realloc` could be expensive even if legal.
        if (resize(ctx, memory, alignment, new_len, return_address)) {
            return memory.ptr;
        }
        switch (allocStrat(alignment)) {
            .raw => {
                // `malloc` and friends guarantee the required alignment, so we can try `realloc`.
                // C only needs to respect `max_align_t` up to the allocation size due to object
                // alignment rules. If necessary, extend the allocation size.
                const actual_len = @max(new_len, @alignOf(std.c.max_align_t));
                const new_ptr = c.realloc(memory.ptr, actual_len) orelse return null;
                assert(alignment.check(@intFromPtr(new_ptr)));
                return @ptrCast(new_ptr);
            },
            .posix_memalign, .manual_align => {
                // `realloc` would potentially return a new allocation which does not respect
                // the original alignment, so we can't do anything more.
                return null;
            },
        }
    }

    fn free(
        _: *anyopaque,
        memory: []u8,
        alignment: Alignment,
        return_address: usize,
    ) void {
        _ = return_address;
        switch (allocStrat(alignment)) {
            .raw, .posix_memalign => c.free(memory.ptr),
            .manual_align => c.free(manualAlignHeader(memory.ptr).*),
        }
    }
};

/// On operating systems that support memory mapping, this allocator makes a
/// syscall directly for every allocation and free.
///
/// Otherwise, it falls back to the preferred singleton for the target.
///
/// Thread-safe.
pub const page_allocator: Allocator = if (@hasDecl(root, "os") and
    @hasDecl(root.os, "heap") and
    @hasDecl(root.os.heap, "page_allocator"))
    root.os.heap.page_allocator
else if (builtin.target.cpu.arch.isWasm()) .{
    .ptr = undefined,
    .vtable = &WasmAllocator.vtable,
} else .{
    .ptr = undefined,
    .vtable = &PageAllocator.vtable,
};

pub const smp_allocator: Allocator = .{
    .ptr = undefined,
    .vtable = &SmpAllocator.vtable,
};

/// This allocator is fast, small, and specific to WebAssembly.
pub const wasm_allocator: Allocator = .{
    .ptr = undefined,
    .vtable = &WasmAllocator.vtable,
};

/// Supports single-threaded WebAssembly and Linux.
pub const brk_allocator: Allocator = .{
    .ptr = undefined,
    .vtable = &BrkAllocator.vtable,
};

/// Returns a `StackFallbackAllocator` allocating using either a
/// `FixedBufferAllocator` on an array of size `size` and falling back to
/// `fallback_allocator` if that fails.
pub fn stackFallback(comptime size: usize, fallback_allocator: Allocator) StackFallbackAllocator(size) {
    return StackFallbackAllocator(size){
        .buffer = undefined,
        .fallback_allocator = fallback_allocator,
        .fixed_buffer_allocator = undefined,
    };
}

/// An allocator that attempts to allocate using a
/// `FixedBufferAllocator` using an array of size `size`. If the
/// allocation fails, it will fall back to using
/// `fallback_allocator`. Easily created with `stackFallback`.
pub fn StackFallbackAllocator(comptime size: usize) type {
    return struct {
        const Self = @This();

        buffer: [size]u8,
        fallback_allocator: Allocator,
        fixed_buffer_allocator: FixedBufferAllocator,
        get_called: if (std.debug.runtime_safety) bool else void =
            if (std.debug.runtime_safety) false else {},

        /// This function both fetches a `Allocator` interface to this
        /// allocator *and* resets the internal buffer allocator.
        pub fn get(self: *Self) Allocator {
            if (std.debug.runtime_safety) {
                assert(!self.get_called); // `get` called multiple times; instead use `const allocator = stackFallback(N).get();`
                self.get_called = true;
            }
            self.fixed_buffer_allocator = FixedBufferAllocator.init(self.buffer[0..]);
            return .{
                .ptr = self,
                .vtable = &.{
                    .alloc = alloc,
                    .resize = resize,
                    .remap = remap,
                    .free = free,
                },
            };
        }

        /// Unlike most std allocators `StackFallbackAllocator` modifies
        /// its internal state before returning an implementation of
        /// the`Allocator` interface and therefore also doesn't use
        /// the usual `.allocator()` method.
        pub const allocator = @compileError("use 'const allocator = stackFallback(N).get();' instead");

        fn alloc(
            ctx: *anyopaque,
            len: usize,
            alignment: Alignment,
            ra: usize,
        ) ?[*]u8 {
            const self: *Self = @ptrCast(@alignCast(ctx));
            return FixedBufferAllocator.alloc(&self.fixed_buffer_allocator, len, alignment, ra) orelse
                return self.fallback_allocator.rawAlloc(len, alignment, ra);
        }

        fn resize(
            ctx: *anyopaque,
            buf: []u8,
            alignment: Alignment,
            new_len: usize,
            ra: usize,
        ) bool {
            const self: *Self = @ptrCast(@alignCast(ctx));
            if (self.fixed_buffer_allocator.ownsPtr(buf.ptr)) {
                return FixedBufferAllocator.resize(&self.fixed_buffer_allocator, buf, alignment, new_len, ra);
            } else {
                return self.fallback_allocator.rawResize(buf, alignment, new_len, ra);
            }
        }

        fn remap(
            context: *anyopaque,
            memory: []u8,
            alignment: Alignment,
            new_len: usize,
            return_address: usize,
        ) ?[*]u8 {
            const self: *Self = @ptrCast(@alignCast(context));
            if (self.fixed_buffer_allocator.ownsPtr(memory.ptr)) {
                return FixedBufferAllocator.remap(&self.fixed_buffer_allocator, memory, alignment, new_len, return_address);
            } else {
                return self.fallback_allocator.rawRemap(memory, alignment, new_len, return_address);
            }
        }

        fn free(
            ctx: *anyopaque,
            buf: []u8,
            alignment: Alignment,
            ra: usize,
        ) void {
            const self: *Self = @ptrCast(@alignCast(ctx));
            if (self.fixed_buffer_allocator.ownsPtr(buf.ptr)) {
                return FixedBufferAllocator.free(&self.fixed_buffer_allocator, buf, alignment, ra);
            } else {
                return self.fallback_allocator.rawFree(buf, alignment, ra);
            }
        }
    };
}

test c_allocator {
    if (builtin.link_libc) {
        try testAllocator(c_allocator);
        try testAllocatorAligned(c_allocator);
        try testAllocatorLargeAlignment(c_allocator);
        try testAllocatorAlignedShrink(c_allocator);
    }
}

test smp_allocator {
    if (builtin.single_threaded) return;
    try testAllocator(smp_allocator);
    try testAllocatorAligned(smp_allocator);
    try testAllocatorLargeAlignment(smp_allocator);
    try testAllocatorAlignedShrink(smp_allocator);
}

test PageAllocator {
    const allocator = page_allocator;
    try testAllocator(allocator);
    try testAllocatorAligned(allocator);
    if (!builtin.target.cpu.arch.isWasm()) {
        try testAllocatorLargeAlignment(allocator);
        try testAllocatorAlignedShrink(allocator);
    }

    if (builtin.os.tag == .windows) {
        const slice = try allocator.alignedAlloc(u8, .fromByteUnits(page_size_min), 128);
        slice[0] = 0x12;
        slice[127] = 0x34;
        allocator.free(slice);
    }
    {
        var buf = try allocator.alloc(u8, pageSize() + 1);
        defer allocator.free(buf);
        buf = try allocator.realloc(buf, 1); // shrink past the page boundary
    }
}

test ArenaAllocator {
    var arena_allocator = ArenaAllocator.init(page_allocator);
    defer arena_allocator.deinit();
    const allocator = arena_allocator.allocator();

    try testAllocator(allocator);
    try testAllocatorAligned(allocator);
    try testAllocatorLargeAlignment(allocator);
    try testAllocatorAlignedShrink(allocator);
}

test "StackFallbackAllocator" {
    {
        var stack_allocator = stackFallback(4096, std.testing.allocator);
        try testAllocator(stack_allocator.get());
    }
    {
        var stack_allocator = stackFallback(4096, std.testing.allocator);
        try testAllocatorAligned(stack_allocator.get());
    }
    {
        var stack_allocator = stackFallback(4096, std.testing.allocator);
        try testAllocatorLargeAlignment(stack_allocator.get());
    }
    {
        var stack_allocator = stackFallback(4096, std.testing.allocator);
        try testAllocatorAlignedShrink(stack_allocator.get());
    }
}

/// This one should not try alignments that exceed what C malloc can handle.
pub fn testAllocator(base_allocator: mem.Allocator) !void {
    var validationAllocator = mem.validationWrap(base_allocator);
    const allocator = validationAllocator.allocator();

    var slice = try allocator.alloc(*i32, 100);
    try testing.expect(slice.len == 100);
    for (slice, 0..) |*item, i| {
        item.* = try allocator.create(i32);
        item.*.* = @as(i32, @intCast(i));
    }

    slice = try allocator.realloc(slice, 20000);
    try testing.expect(slice.len == 20000);

    for (slice[0..100], 0..) |item, i| {
        try testing.expect(item.* == @as(i32, @intCast(i)));
        allocator.destroy(item);
    }

    if (allocator.resize(slice, 50)) {
        slice = slice[0..50];
        if (allocator.resize(slice, 25)) {
            slice = slice[0..25];
            try testing.expect(allocator.resize(slice, 0));
            slice = slice[0..0];
            slice = try allocator.realloc(slice, 10);
            try testing.expect(slice.len == 10);
        }
    }
    allocator.free(slice);

    // Zero-length allocation
    const empty = try allocator.alloc(u8, 0);
    allocator.free(empty);
    // Allocation with zero-sized types
    const zero_bit_ptr = try allocator.create(u0);
    zero_bit_ptr.* = 0;
    allocator.destroy(zero_bit_ptr);
    const zero_len_array = try allocator.create([0]u64);
    allocator.destroy(zero_len_array);

    const oversize = try allocator.alignedAlloc(u32, null, 5);
    try testing.expect(oversize.len >= 5);
    for (oversize) |*item| {
        item.* = 0xDEADBEEF;
    }
    allocator.free(oversize);
}

pub fn testAllocatorAligned(base_allocator: mem.Allocator) !void {
    var validationAllocator = mem.validationWrap(base_allocator);
    const allocator = validationAllocator.allocator();

    // Test a few alignment values, smaller and bigger than the type's one
    inline for ([_]Alignment{ .@"1", .@"2", .@"4", .@"8", .@"16", .@"32", .@"64" }) |alignment| {
        // initial
        var slice = try allocator.alignedAlloc(u8, alignment, 10);
        try testing.expect(slice.len == 10);
        // grow
        slice = try allocator.realloc(slice, 100);
        try testing.expect(slice.len == 100);
        if (allocator.resize(slice, 10)) {
            slice = slice[0..10];
        }
        try testing.expect(allocator.resize(slice, 0));
        slice = slice[0..0];
        // realloc from zero
        slice = try allocator.realloc(slice, 100);
        try testing.expect(slice.len == 100);
        if (allocator.resize(slice, 10)) {
            slice = slice[0..10];
        }
        try testing.expect(allocator.resize(slice, 0));
    }
}

pub fn testAllocatorLargeAlignment(base_allocator: mem.Allocator) !void {
    var validationAllocator = mem.validationWrap(base_allocator);
    const allocator = validationAllocator.allocator();

    const large_align: usize = page_size_min / 2;

    var align_mask: usize = undefined;
    align_mask = @shlWithOverflow(~@as(usize, 0), @as(Allocator.Log2Align, @ctz(large_align)))[0];

    var slice = try allocator.alignedAlloc(u8, .fromByteUnits(large_align), 500);
    try testing.expect(@intFromPtr(slice.ptr) & align_mask == @intFromPtr(slice.ptr));

    if (allocator.resize(slice, 100)) {
        slice = slice[0..100];
    }

    slice = try allocator.realloc(slice, 5000);
    try testing.expect(@intFromPtr(slice.ptr) & align_mask == @intFromPtr(slice.ptr));

    if (allocator.resize(slice, 10)) {
        slice = slice[0..10];
    }

    slice = try allocator.realloc(slice, 20000);
    try testing.expect(@intFromPtr(slice.ptr) & align_mask == @intFromPtr(slice.ptr));

    allocator.free(slice);
}

pub fn testAllocatorAlignedShrink(base_allocator: mem.Allocator) !void {
    var validationAllocator = mem.validationWrap(base_allocator);
    const allocator = validationAllocator.allocator();

    var debug_buffer: [1000]u8 = undefined;
    var fib = FixedBufferAllocator.init(&debug_buffer);
    const debug_allocator = fib.allocator();

    const alloc_size = pageSize() * 2 + 50;
    var slice = try allocator.alignedAlloc(u8, .@"16", alloc_size);
    defer allocator.free(slice);

    var stuff_to_free = std.array_list.Managed([]align(16) u8).init(debug_allocator);
    // On Windows, VirtualAlloc returns addresses aligned to a 64K boundary,
    // which is 16 pages, hence the 32. This test may require to increase
    // the size of the allocations feeding the `allocator` parameter if they
    // fail, because of this high over-alignment we want to have.
    while (@intFromPtr(slice.ptr) == mem.alignForward(usize, @intFromPtr(slice.ptr), pageSize() * 32)) {
        try stuff_to_free.append(slice);
        slice = try allocator.alignedAlloc(u8, .@"16", alloc_size);
    }
    while (stuff_to_free.pop()) |item| {
        allocator.free(item);
    }
    slice[0] = 0x12;
    slice[60] = 0x34;

    slice = try allocator.reallocAdvanced(slice, alloc_size / 2, 0);
    try testing.expect(slice[0] == 0x12);
    try testing.expect(slice[60] == 0x34);
}

const page_size_min_default: ?usize = switch (builtin.os.tag) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => switch (builtin.cpu.arch) {
        .x86_64 => 4 << 10,
        .aarch64 => 16 << 10,
        else => null,
    },
    .windows => switch (builtin.cpu.arch) {
        // -- <https://devblogs.microsoft.com/oldnewthing/20210510-00/?p=105200>
        .x86, .x86_64 => 4 << 10,
        // SuperH => 4 << 10,
        .mips, .mipsel, .mips64, .mips64el => 4 << 10,
        .powerpc, .powerpcle, .powerpc64, .powerpc64le => 4 << 10,
        // DEC Alpha => 8 << 10,
        // Itanium => 8 << 10,
        .thumb, .thumbeb, .arm, .armeb, .aarch64, .aarch64_be => 4 << 10,
        else => null,
    },
    .wasi => switch (builtin.cpu.arch) {
        .wasm32, .wasm64 => 64 << 10,
        else => null,
    },
    // https://github.com/tianocore/edk2/blob/b158dad150bf02879668f72ce306445250838201/MdePkg/Include/Uefi/UefiBaseType.h#L180-L187
    .uefi => 4 << 10,
    .freebsd => switch (builtin.cpu.arch) {
        // FreeBSD/sys/*
        .x86, .x86_64 => 4 << 10,
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 4 << 10,
        .riscv32, .riscv64 => 4 << 10,
        else => null,
    },
    .netbsd => switch (builtin.cpu.arch) {
        // NetBSD/sys/arch/*
        .alpha => 8 << 10,
        .x86, .x86_64 => 4 << 10,
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        .hppa => 4 << 10,
        .mips, .mipsel, .mips64, .mips64el => 4 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 4 << 10,
        .sh, .sheb => 4 << 10,
        .sparc => 4 << 10,
        .sparc64 => 8 << 10,
        .riscv32, .riscv64 => 4 << 10,
        // Sun-2
        .m68k => 2 << 10,
        else => null,
    },
    .dragonfly => switch (builtin.cpu.arch) {
        .x86, .x86_64 => 4 << 10,
        else => null,
    },
    .openbsd => switch (builtin.cpu.arch) {
        // OpenBSD/sys/arch/*
        .alpha => 8 << 10,
        .hppa => 4 << 10,
        .x86, .x86_64 => 4 << 10,
        .thumb, .thumbeb, .arm, .armeb, .aarch64, .aarch64_be => 4 << 10,
        .mips64, .mips64el => 4 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 4 << 10,
        .riscv64 => 4 << 10,
        .sh, .sheb => 4 << 10,
        .sparc64 => 8 << 10,
        else => null,
    },
    .illumos => switch (builtin.cpu.arch) {
        // src/uts/*/sys/machparam.h
        .x86, .x86_64 => 4 << 10,
        .sparc, .sparc64 => 8 << 10,
        else => null,
    },
    .fuchsia => switch (builtin.cpu.arch) {
        // fuchsia/kernel/arch/*/include/arch/defines.h
        .x86_64 => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        .riscv64 => 4 << 10,
        else => null,
    },
    // https://github.com/SerenityOS/serenity/blob/62b938b798dc009605b5df8a71145942fc53808b/Kernel/API/POSIX/sys/limits.h#L11-L13
    .serenity => 4 << 10,
    .haiku => switch (builtin.cpu.arch) {
        // haiku/headers/posix/arch/*/limits.h
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        .m68k => 4 << 10,
        .mips, .mipsel, .mips64, .mips64el => 4 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 4 << 10,
        .riscv64 => 4 << 10,
        .sparc64 => 8 << 10,
        .x86, .x86_64 => 4 << 10,
        else => null,
    },
    .hurd => switch (builtin.cpu.arch) {
        // gnumach/*/include/mach/*/vm_param.h
        .x86, .x86_64 => 4 << 10,
        .aarch64 => null,
        else => null,
    },
    .plan9 => switch (builtin.cpu.arch) {
        // 9front/sys/src/9/*/mem.h
        .x86, .x86_64 => 4 << 10,
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        .mips, .mipsel, .mips64, .mips64el => 4 << 10,
        .powerpc, .powerpcle, .powerpc64, .powerpc64le => 4 << 10,
        .sparc => 4 << 10,
        else => null,
    },
    .ps3 => switch (builtin.cpu.arch) {
        // cell/SDK_doc/en/html/C_and_C++_standard_libraries/stdlib.html
        .powerpc64 => 1 << 20, // 1 MiB
        else => null,
    },
    .ps4 => switch (builtin.cpu.arch) {
        // https://github.com/ps4dev/ps4sdk/blob/4df9d001b66ae4ec07d9a51b62d1e4c5e270eecc/include/machine/param.h#L95
        .x86, .x86_64 => 4 << 10,
        else => null,
    },
    .ps5 => switch (builtin.cpu.arch) {
        // https://github.com/PS5Dev/PS5SDK/blob/a2e03a2a0231a3a3397fa6cd087a01ca6d04f273/include/machine/param.h#L95
        .x86, .x86_64 => 16 << 10,
        else => null,
    },
    .psp => switch (builtin.cpu.arch) {
        // minimum block allocation by testing sceKernel
        .mips, .mipsel => 1 << 8, // 256
        else => null,
    },
    // system/lib/libc/musl/arch/emscripten/bits/limits.h
    .emscripten => 64 << 10,
    .linux => switch (builtin.cpu.arch) {
        // Linux/arch/*/Kconfig
        .alpha => 8 << 10,
        .arc, .arceb => 4 << 10,
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        .csky => 4 << 10,
        .hexagon => 4 << 10,
        .hppa => 4 << 10,
        .loongarch32, .loongarch64 => 4 << 10,
        .m68k => 4 << 10,
        .microblaze, .microblazeel => 4 << 10,
        .mips, .mipsel, .mips64, .mips64el => 4 << 10,
        .or1k => 8 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 4 << 10,
        .riscv32, .riscv64 => 4 << 10,
        .s390x => 4 << 10,
        .sh, .sheb => 4 << 10,
        .sparc => 4 << 10,
        .sparc64 => 8 << 10,
        .x86, .x86_64 => 4 << 10,
        .xtensa, .xtensaeb => 4 << 10,
        else => null,
    },
    .freestanding, .other => switch (builtin.cpu.arch) {
        .wasm32, .wasm64 => 64 << 10,
        .x86, .x86_64 => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        else => null,
    },
    else => null,
};

const page_size_max_default: ?usize = switch (builtin.os.tag) {
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => switch (builtin.cpu.arch) {
        .x86_64 => 4 << 10,
        .aarch64 => 16 << 10,
        else => null,
    },
    .windows => switch (builtin.cpu.arch) {
        // -- <https://devblogs.microsoft.com/oldnewthing/20210510-00/?p=105200>
        .x86, .x86_64 => 4 << 10,
        // SuperH => 4 << 10,
        .mips, .mipsel, .mips64, .mips64el => 4 << 10,
        .powerpc, .powerpcle, .powerpc64, .powerpc64le => 4 << 10,
        // DEC Alpha => 8 << 10,
        // Itanium => 8 << 10,
        .thumb, .thumbeb, .arm, .armeb, .aarch64, .aarch64_be => 4 << 10,
        else => null,
    },
    .wasi => switch (builtin.cpu.arch) {
        .wasm32, .wasm64 => 64 << 10,
        else => null,
    },
    // https://github.com/tianocore/edk2/blob/b158dad150bf02879668f72ce306445250838201/MdePkg/Include/Uefi/UefiBaseType.h#L180-L187
    .uefi => 4 << 10,
    .freebsd => switch (builtin.cpu.arch) {
        // FreeBSD/sys/*
        .x86, .x86_64 => 4 << 10,
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 4 << 10,
        .riscv32, .riscv64 => 4 << 10,
        else => null,
    },
    .netbsd => switch (builtin.cpu.arch) {
        // NetBSD/sys/arch/*
        .alpha => 8 << 10,
        .x86, .x86_64 => 4 << 10,
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 64 << 10,
        .hppa => 4 << 10,
        .mips, .mipsel, .mips64, .mips64el => 16 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 16 << 10,
        .sh, .sheb => 4 << 10,
        .sparc => 8 << 10,
        .sparc64 => 8 << 10,
        .riscv32, .riscv64 => 4 << 10,
        .m68k => 8 << 10,
        else => null,
    },
    .dragonfly => switch (builtin.cpu.arch) {
        .x86, .x86_64 => 4 << 10,
        else => null,
    },
    .openbsd => switch (builtin.cpu.arch) {
        // OpenBSD/sys/arch/*
        .alpha => 8 << 10,
        .hppa => 4 << 10,
        .x86, .x86_64 => 4 << 10,
        .thumb, .thumbeb, .arm, .armeb, .aarch64, .aarch64_be => 4 << 10,
        .mips64, .mips64el => 16 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 4 << 10,
        .riscv64 => 4 << 10,
        .sh, .sheb => 4 << 10,
        .sparc64 => 8 << 10,
        else => null,
    },
    .illumos => switch (builtin.cpu.arch) {
        // src/uts/*/sys/machparam.h
        .x86, .x86_64 => 4 << 10,
        .sparc, .sparc64 => 8 << 10,
        else => null,
    },
    .fuchsia => switch (builtin.cpu.arch) {
        // fuchsia/kernel/arch/*/include/arch/defines.h
        .x86_64 => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        .riscv64 => 4 << 10,
        else => null,
    },
    // https://github.com/SerenityOS/serenity/blob/62b938b798dc009605b5df8a71145942fc53808b/Kernel/API/POSIX/sys/limits.h#L11-L13
    .serenity => 4 << 10,
    .haiku => switch (builtin.cpu.arch) {
        // haiku/headers/posix/arch/*/limits.h
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 4 << 10,
        .m68k => 4 << 10,
        .mips, .mipsel, .mips64, .mips64el => 4 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 4 << 10,
        .riscv64 => 4 << 10,
        .sparc64 => 8 << 10,
        .x86, .x86_64 => 4 << 10,
        else => null,
    },
    .hurd => switch (builtin.cpu.arch) {
        // gnumach/*/include/mach/*/vm_param.h
        .x86, .x86_64 => 4 << 10,
        .aarch64 => null,
        else => null,
    },
    .plan9 => switch (builtin.cpu.arch) {
        // 9front/sys/src/9/*/mem.h
        .x86, .x86_64 => 4 << 10,
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 64 << 10,
        .mips, .mipsel, .mips64, .mips64el => 16 << 10,
        .powerpc, .powerpcle, .powerpc64, .powerpc64le => 4 << 10,
        .sparc => 4 << 10,
        else => null,
    },
    .ps3 => switch (builtin.cpu.arch) {
        // cell/SDK_doc/en/html/C_and_C++_standard_libraries/stdlib.html
        .powerpc64 => 1 << 20, // 1 MiB
        else => null,
    },
    .ps4 => switch (builtin.cpu.arch) {
        // https://github.com/ps4dev/ps4sdk/blob/4df9d001b66ae4ec07d9a51b62d1e4c5e270eecc/include/machine/param.h#L95
        .x86, .x86_64 => 4 << 10,
        else => null,
    },
    .ps5 => switch (builtin.cpu.arch) {
        // https://github.com/PS5Dev/PS5SDK/blob/a2e03a2a0231a3a3397fa6cd087a01ca6d04f273/include/machine/param.h#L95
        .x86, .x86_64 => 16 << 10,
        else => null,
    },
    .psp => switch (builtin.cpu.arch) {
        // minimum block allocation by testing sceKernel
        .mips, .mipsel => 1 << 8, // 256
        else => null,
    },
    // system/lib/libc/musl/arch/emscripten/bits/limits.h
    .emscripten => 64 << 10,
    .linux => switch (builtin.cpu.arch) {
        // Linux/arch/*/Kconfig
        .alpha => 8 << 10,
        .arc, .arceb => 16 << 10,
        .thumb, .thumbeb, .arm, .armeb => 4 << 10,
        .aarch64, .aarch64_be => 64 << 10,
        .csky => 4 << 10,
        .hexagon => 256 << 10,
        .hppa => 64 << 10,
        .loongarch32, .loongarch64 => 64 << 10,
        .m68k => 8 << 10,
        .microblaze, .microblazeel => 4 << 10,
        .mips, .mipsel, .mips64, .mips64el => 64 << 10,
        .or1k => 8 << 10,
        .powerpc, .powerpc64, .powerpc64le, .powerpcle => 256 << 10,
        .riscv32, .riscv64 => 4 << 10,
        .s390x => 4 << 10,
        .sh, .sheb => 64 << 10,
        .sparc => 4 << 10,
        .sparc64 => 8 << 10,
        .x86, .x86_64 => 4 << 10,
        .xtensa, .xtensaeb => 4 << 10,
        else => null,
    },
    .freestanding => switch (builtin.cpu.arch) {
        .wasm32, .wasm64 => 64 << 10,
        else => null,
    },
    else => null,
};

test {
    _ = @import("heap/memory_pool.zig");
    _ = ArenaAllocator;
    _ = DebugAllocator(.{});
    _ = FixedBufferAllocator;
    if (builtin.single_threaded) {
        if (builtin.cpu.arch.isWasm() or (builtin.os.tag == .linux and !builtin.link_libc)) {
            _ = brk_allocator;
        }
    } else {
        _ = smp_allocator;
    }
}



---
File: /std/http.zig
---

const builtin = @import("builtin");
const std = @import("std.zig");
const assert = std.debug.assert;
const Writer = std.Io.Writer;
const File = std.Io.File;

pub const Client = @import("http/Client.zig");
pub const Server = @import("http/Server.zig");
pub const HeadParser = @import("http/HeadParser.zig");
pub const ChunkParser = @import("http/ChunkParser.zig");
pub const HeaderIterator = @import("http/HeaderIterator.zig");

pub const Version = enum {
    @"HTTP/1.0",
    @"HTTP/1.1",
};

/// https://developer.mozilla.org/en-US/docs/Web/HTTP/Methods
///
/// https://datatracker.ietf.org/doc/html/rfc7231#section-4 Initial definition
///
/// https://datatracker.ietf.org/doc/html/rfc5789#section-2 PATCH
pub const Method = enum {
    GET,
    HEAD,
    POST,
    PUT,
    DELETE,
    CONNECT,
    OPTIONS,
    TRACE,
    PATCH,

    /// Returns true if a request of this method is allowed to have a body
    /// Actual behavior from servers may vary and should still be checked
    pub fn requestHasBody(m: Method) bool {
        return switch (m) {
            .POST, .PUT, .PATCH => true,
            .GET, .HEAD, .DELETE, .CONNECT, .OPTIONS, .TRACE => false,
        };
    }

    /// Returns true if a response to this method is allowed to have a body
    /// Actual behavior from clients may vary and should still be checked
    pub fn responseHasBody(m: Method) bool {
        return switch (m) {
            .GET, .POST, .PUT, .DELETE, .CONNECT, .OPTIONS, .PATCH => true,
            .HEAD, .TRACE => false,
        };
    }

    /// An HTTP method is safe if it doesn't alter the state of the server.
    ///
    /// https://developer.mozilla.org/en-US/docs/Glossary/Safe/HTTP
    ///
    /// https://datatracker.ietf.org/doc/html/rfc7231#section-4.2.1
    pub fn safe(m: Method) bool {
        return switch (m) {
            .GET, .HEAD, .OPTIONS, .TRACE => true,
            .POST, .PUT, .DELETE, .CONNECT, .PATCH => false,
        };
    }

    /// An HTTP method is idempotent if an identical request can be made once
    /// or several times in a row with the same effect while leaving the server
    /// in the same state.
    ///
    /// https://developer.mozilla.org/en-US/docs/Glossary/Idempotent
    ///
    /// https://datatracker.ietf.org/doc/html/rfc7231#section-4.2.2
    pub fn idempotent(m: Method) bool {
        return switch (m) {
            .GET, .HEAD, .PUT, .DELETE, .OPTIONS, .TRACE => true,
            .CONNECT, .POST, .PATCH => false,
        };
    }

    /// A cacheable response can be stored to be retrieved and used later,
    /// saving a new request to the server.
    ///
    /// https://developer.mozilla.org/en-US/docs/Glossary/cacheable
    ///
    /// https://datatracker.ietf.org/doc/html/rfc7231#section-4.2.3
    pub fn cacheable(m: Method) bool {
        return switch (m) {
            .GET, .HEAD => true,
            .POST, .PUT, .DELETE, .CONNECT, .OPTIONS, .TRACE, .PATCH => false,
        };
    }
};

/// https://developer.mozilla.org/en-US/docs/Web/HTTP/Status
pub const Status = enum(u10) {
    @"continue" = 100, // RFC7231, Section 6.2.1
    switching_protocols = 101, // RFC7231, Section 6.2.2
    processing = 102, // RFC2518
    early_hints = 103, // RFC8297

    ok = 200, // RFC7231, Section 6.3.1
    created = 201, // RFC7231, Section 6.3.2
    accepted = 202, // RFC7231, Section 6.3.3
    non_authoritative_info = 203, // RFC7231, Section 6.3.4
    no_content = 204, // RFC7231, Section 6.3.5
    reset_content = 205, // RFC7231, Section 6.3.6
    partial_content = 206, // RFC7233, Section 4.1
    multi_status = 207, // RFC4918
    already_reported = 208, // RFC5842
    im_used = 226, // RFC3229

    multiple_choice = 300, // RFC7231, Section 6.4.1
    moved_permanently = 301, // RFC7231, Section 6.4.2
    found = 302, // RFC7231, Section 6.4.3
    see_other = 303, // RFC7231, Section 6.4.4
    not_modified = 304, // RFC7232, Section 4.1
    use_proxy = 305, // RFC7231, Section 6.4.5
    temporary_redirect = 307, // RFC7231, Section 6.4.7
    permanent_redirect = 308, // RFC7538

    bad_request = 400, // RFC7231, Section 6.5.1
    unauthorized = 401, // RFC7235, Section 3.1
    payment_required = 402, // RFC7231, Section 6.5.2
    forbidden = 403, // RFC7231, Section 6.5.3
    not_found = 404, // RFC7231, Section 6.5.4
    method_not_allowed = 405, // RFC7231, Section 6.5.5
    not_acceptable = 406, // RFC7231, Section 6.5.6
    proxy_auth_required = 407, // RFC7235, Section 3.2
    request_timeout = 408, // RFC7231, Section 6.5.7
    conflict = 409, // RFC7231, Section 6.5.8
    gone = 410, // RFC7231, Section 6.5.9
    length_required = 411, // RFC7231, Section 6.5.10
    precondition_failed = 412, // RFC7232, Section 4.2][RFC8144, Section 3.2
    payload_too_large = 413, // RFC7231, Section 6.5.11
    uri_too_long = 414, // RFC7231, Section 6.5.12
    unsupported_media_type = 415, // RFC7231, Section 6.5.13][RFC7694, Section 3
    range_not_satisfiable = 416, // RFC7233, Section 4.4
    expectation_failed = 417, // RFC7231, Section 6.5.14
    teapot = 418, // RFC 7168, 2.3.3
    misdirected_request = 421, // RFC7540, Section 9.1.2
    unprocessable_entity = 422, // RFC4918
    locked = 423, // RFC4918
    failed_dependency = 424, // RFC4918
    too_early = 425, // RFC8470
    upgrade_required = 426, // RFC7231, Section 6.5.15
    precondition_required = 428, // RFC6585
    too_many_requests = 429, // RFC6585
    request_header_fields_too_large = 431, // RFC6585
    unavailable_for_legal_reasons = 451, // RFC7725

    internal_server_error = 500, // RFC7231, Section 6.6.1
    not_implemented = 501, // RFC7231, Section 6.6.2
    bad_gateway = 502, // RFC7231, Section 6.6.3
    service_unavailable = 503, // RFC7231, Section 6.6.4
    gateway_timeout = 504, // RFC7231, Section 6.6.5
    http_version_not_support
```
