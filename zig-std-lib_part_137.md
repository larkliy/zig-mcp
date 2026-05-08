```

        /// Insert `item` at index `i`. Moves `list[i .. list.len]` to higher indices to make room.
        ///
        /// If `i` is equal to the length of the list this operation is equivalent to append.
        ///
        /// This operation is O(N).
        ///
        /// Asserts that the list has capacity for one additional item.
        ///
        /// Asserts that the index is in bounds or equal to the length.
        pub fn insertAssumeCapacity(self: *Self, i: usize, item: T) void {
            assert(self.items.len < self.capacity);
            self.items.len += 1;

            @memmove(self.items[i + 1 .. self.items.len], self.items[i .. self.items.len - 1]);
            self.items[i] = item;
        }

        /// Insert `item` at index `i`, moving `list[i .. list.len]` to higher indices to make room.
        ///
        /// If `i` is equal to the length of the list this operation is equivalent to append.
        ///
        /// This operation is O(N).
        ///
        /// If the list lacks unused capacity for the additional item, returns
        /// `error.OutOfMemory`.
        ///
        /// Asserts that the index is in bounds or equal to the length.
        pub fn insertBounded(self: *Self, i: usize, item: T) error{OutOfMemory}!void {
            if (self.capacity - self.items.len == 0) return error.OutOfMemory;
            return insertAssumeCapacity(self, i, item);
        }

        /// Add `count` new elements at position `index`, which have
        /// `undefined` values. Returns a slice pointing to the newly allocated
        /// elements, which becomes invalid after various `ArrayList`
        /// operations.
        /// Invalidates pre-existing pointers to elements at and after `index`.
        /// Invalidates all pre-existing element pointers if capacity must be
        /// increased to accommodate the new elements.
        /// Asserts that the index is in bounds or equal to the length.
        pub fn addManyAt(
            self: *Self,
            gpa: Allocator,
            index: usize,
            count: usize,
        ) Allocator.Error![]T {
            var managed = self.toManaged(gpa);
            defer self.* = managed.moveToUnmanaged();
            return managed.addManyAt(index, count);
        }

        /// Add `count` new elements at position `index`, which have
        /// `undefined` values. Returns a slice pointing to the newly allocated
        /// elements, which becomes invalid after various `ArrayList`
        /// operations.
        /// Invalidates pre-existing pointers to elements at and after `index`, but
        /// does not invalidate any before that.
        /// Asserts that the list has capacity for the additional items.
        /// Asserts that the index is in bounds or equal to the length.
        pub fn addManyAtAssumeCapacity(self: *Self, index: usize, count: usize) []T {
            const new_len = self.items.len + count;
            assert(self.capacity >= new_len);
            const to_move = self.items[index..];
            self.items.len = new_len;
            @memmove(self.items[index + count ..][0..to_move.len], to_move);
            const result = self.items[index..][0..count];
            @memset(result, undefined);
            return result;
        }

        /// Add `count` new elements at position `index`, which have
        /// `undefined` values, returning a slice pointing to the newly
        /// allocated elements, which becomes invalid after various `ArrayList`
        /// operations.
        ///
        /// Invalidates pre-existing pointers to elements at and after `index`, but
        /// does not invalidate any before that.
        ///
        /// If the list lacks unused capacity for the additional items, returns
        /// `error.OutOfMemory`.
        ///
        /// Asserts that the index is in bounds or equal to the length.
        pub fn addManyAtBounded(self: *Self, index: usize, count: usize) error{OutOfMemory}![]T {
            if (self.capacity - self.items.len < count) return error.OutOfMemory;
            return addManyAtAssumeCapacity(self, index, count);
        }

        /// Insert slice `items` at index `i` by moving `list[i .. list.len]` to make room.
        /// This operation is O(N).
        /// Invalidates pre-existing pointers to elements at and after `index`.
        /// Invalidates all pre-existing element pointers if capacity must be
        /// increased to accommodate the new elements.
        /// Asserts that the index is in bounds or equal to the length.
        pub fn insertSlice(
            self: *Self,
            gpa: Allocator,
            index: usize,
            items: []const T,
        ) Allocator.Error!void {
            const dst = try self.addManyAt(
                gpa,
                index,
                items.len,
            );
            @memcpy(dst, items);
        }

        /// Insert slice `items` at index `i` by moving `list[i .. list.len]` to make room.
        /// This operation is O(N).
        /// Invalidates pre-existing pointers to elements at and after `index`.
        /// Asserts that the list has capacity for the additional items.
        /// Asserts that the index is in bounds or equal to the length.
        pub fn insertSliceAssumeCapacity(
            self: *Self,
            index: usize,
            items: []const T,
        ) void {
            const dst = self.addManyAtAssumeCapacity(index, items.len);
            @memcpy(dst, items);
        }

        /// Insert slice `items` at index `i` by moving `list[i .. list.len]` to make room.
        /// This operation is O(N).
        /// Invalidates pre-existing pointers to elements at and after `index`.
        /// If the list lacks unused capacity for the additional items, returns
        /// `error.OutOfMemory`.
        /// Asserts that the index is in bounds or equal to the length.
        pub fn insertSliceBounded(
            self: *Self,
            index: usize,
            items: []const T,
        ) error{OutOfMemory}!void {
            const dst = try self.addManyAtBounded(index, items.len);
            @memcpy(dst, items);
        }

        /// Grows or shrinks the list as necessary.
        /// Invalidates element pointers if additional capacity is allocated.
        /// Asserts that the range is in bounds.
        pub fn replaceRange(
            self: *Self,
            gpa: Allocator,
            start: usize,
            len: usize,
            new_items: []const T,
        ) Allocator.Error!void {
            try self.ensureTotalCapacity(gpa, try addOrOom(self.items.len - len, new_items.len));
            self.replaceRangeAssumeCapacity(start, len, new_items);
        }

        /// Grows or shrinks the list as necessary.
        ///
        /// Never invalidates element pointers.
        ///
        /// Asserts the capacity is enough for additional items.
        pub fn replaceRangeAssumeCapacity(
            self: *Self,
            start: usize,
            len: usize,
            new_items: []const T,
        ) void {
            std.debug.assert(self.capacity - self.items.len >= new_items.len -| len);

            const tail = self.items[start + len ..];
            const vacated = self.items[self.items.len - (len -| new_items.len) ..];
            self.items.len = self.items.len - len + new_items.len;
            @memmove(self.items[start + new_items.len ..], tail);
            @memcpy(self.items[start..][0..new_items.len], new_items);
            @memset(vacated, undefined);
        }

        /// Grows or shrinks the list as necessary.
        ///
        /// Never invalidates element pointers.
        ///
        /// If the unused capacity is insufficient for additional items,
        /// returns `error.OutOfMemory`.
        pub fn replaceRangeBounded(
            self: *Self,
            start: usize,
            len: usize,
            new_items: []const T,
        ) error{OutOfMemory}!void {
            if (self.capacity - self.items.len < new_items.len -| len) return error.OutOfMemory;
            return replaceRangeAssumeCapacity(self, start, len, new_items);
        }

        /// Extend the list by 1 element. Allocates more memory as necessary.
        /// Invalidates element pointers if additional memory is needed.
        pub fn append(self: *Self, gpa: Allocator, item: T) Allocator.Error!void {
            const new_item_ptr = try self.addOne(gpa);
            new_item_ptr.* = item;
        }

        /// Extend the list by 1 element.
        ///
        /// Never invalidates element pointers.
        ///
        /// Asserts that the list can hold one additional item.
        pub fn appendAssumeCapacity(self: *Self, item: T) void {
            self.addOneAssumeCapacity().* = item;
        }

        /// Extend the list by 1 element.
        ///
        /// Never invalidates element pointers.
        ///
        /// If the list lacks unused capacity for the additional item, returns
        /// `error.OutOfMemory`.
        pub fn appendBounded(self: *Self, item: T) error{OutOfMemory}!void {
            if (self.capacity - self.items.len == 0) return error.OutOfMemory;
            return appendAssumeCapacity(self, item);
        }

        /// Remove the element at index `i` from the list and return its value.
        /// Invalidates pointers to the last element.
        /// This operation is O(N).
        /// Asserts that the index is in bounds.
        pub fn orderedRemove(self: *Self, i: usize) T {
            const old_item = self.items[i];
            self.replaceRangeAssumeCapacity(i, 1, &.{});
            return old_item;
        }

        /// Remove the elements indexed by `sorted_indexes`. The indexes to be
        /// removed correspond to the array list before deletion.
        ///
        /// Asserts:
        /// * Each index to be removed is in bounds.
        /// * The indexes to be removed are sorted ascending.
        ///
        /// Duplicates in `sorted_indexes` are allowed.
        ///
        /// This operation is O(N).
        ///
        /// Invalidates element pointers beyond the first deleted index.
        pub fn orderedRemoveMany(self: *Self, sorted_indexes: []const usize) void {
            if (sorted_indexes.len == 0) return;
            var shift: usize = 1;
            for (sorted_indexes[0 .. sorted_indexes.len - 1], sorted_indexes[1..]) |removed, end| {
                if (removed == end) continue; // allows duplicates in `sorted_indexes`
                const start = removed + 1;
                const len = end - start; // safety checks `sorted_indexes` are sorted
                @memmove(self.items[start - shift ..][0..len], self.items[start..][0..len]); // safety checks initial `sorted_indexes` are in range
                shift += 1;
            }
            const start = sorted_indexes[sorted_indexes.len - 1] + 1;
            const end = self.items.len;
            const len = end - start; // safety checks final `sorted_indexes` are in range
            @memmove(self.items[start - shift ..][0..len], self.items[start..][0..len]);
            self.items.len = end - shift;
        }

        /// Removes the element at the specified index and returns it.
        /// The empty slot is filled from the end of the list.
        /// Invalidates pointers to last element.
        /// This operation is O(1).
        /// Asserts that the index is in bounds.
        pub fn swapRemove(self: *Self, i: usize) T {
            const val = self.items[i];
            self.items[i] = self.items[self.items.len - 1];
            self.items[self.items.len - 1] = undefined;
            self.items.len -= 1;
            return val;
        }

        /// Append the slice of items to the list. Allocates more
        /// memory as necessary.
        /// Invalidates element pointers if additional memory is needed.
        pub fn appendSlice(self: *Self, gpa: Allocator, items: []const T) Allocator.Error!void {
            try self.ensureUnusedCapacity(gpa, items.len);
            self.appendSliceAssumeCapacity(items);
        }

        /// Append the slice of items to the list.
        ///
        /// Asserts that the list can hold the additional items.
        pub fn appendSliceAssumeCapacity(self: *Self, items: []const T) void {
            const old_len = self.items.len;
            const new_len = old_len + items.len;
            assert(new_len <= self.capacity);
            self.items.len = new_len;
            @memcpy(self.items[old_len..][0..items.len], items);
        }

        /// Append the slice of items to the list.
        ///
        /// If the list lacks unused capacity for the additional items, returns `error.OutOfMemory`.
        pub fn appendSliceBounded(self: *Self, items: []const T) error{OutOfMemory}!void {
            if (self.capacity - self.items.len < items.len) return error.OutOfMemory;
            return appendSliceAssumeCapacity(self, items);
        }

        /// Append the slice of items to the list. Allocates more
        /// memory as necessary. Only call this function if a call to `appendSlice` instead would
        /// be a compile error.
        /// Invalidates element pointers if additional memory is needed.
        pub fn appendUnalignedSlice(self: *Self, gpa: Allocator, items: []align(1) const T) Allocator.Error!void {
            try self.ensureUnusedCapacity(gpa, items.len);
            self.appendUnalignedSliceAssumeCapacity(items);
        }

        /// Append an unaligned slice of items to the list.
        ///
        /// Intended to be used only when `appendSliceAssumeCapacity` would be
        /// a compile error.
        ///
        /// Asserts that the list can hold the additional items.
        pub fn appendUnalignedSliceAssumeCapacity(self: *Self, items: []align(1) const T) void {
            const old_len = self.items.len;
            const new_len = old_len + items.len;
            assert(new_len <= self.capacity);
            self.items.len = new_len;
            @memcpy(self.items[old_len..][0..items.len], items);
        }

        /// Append an unaligned slice of items to the list.
        ///
        /// Intended to be used only when `appendSliceAssumeCapacity` would be
        /// a compile error.
        ///
        /// If the list lacks unused capacity for the additional items, returns
        /// `error.OutOfMemory`.
        pub fn appendUnalignedSliceBounded(self: *Self, items: []align(1) const T) error{OutOfMemory}!void {
            if (self.capacity - self.items.len < items.len) return error.OutOfMemory;
            return appendUnalignedSliceAssumeCapacity(self, items);
        }

        pub fn print(self: *Self, gpa: Allocator, comptime fmt: []const u8, args: anytype) error{OutOfMemory}!void {
            comptime assert(T == u8);
            try self.ensureUnusedCapacity(gpa, fmt.len);
            var aw: std.Io.Writer.Allocating = .fromArrayList(gpa, self);
            defer self.* = aw.toArrayList();
            return aw.writer.print(fmt, args) catch |err| switch (err) {
                error.WriteFailed => return error.OutOfMemory,
            };
        }

        pub fn printAssumeCapacity(self: *Self, comptime fmt: []const u8, args: anytype) void {
            comptime assert(T == u8);
            var w: std.Io.Writer = .fixed(self.unusedCapacitySlice());
            w.print(fmt, args) catch unreachable;
            self.items.len += w.end;
        }

        pub fn printBounded(self: *Self, comptime fmt: []const u8, args: anytype) error{OutOfMemory}!void {
            comptime assert(T == u8);
            var w: std.Io.Writer = .fixed(self.unusedCapacitySlice());
            w.print(fmt, args) catch return error.OutOfMemory;
            self.items.len += w.end;
        }

        /// Append a value to the list `n` times.
        /// Allocates more memory as necessary.
        /// Invalidates element pointers if additional memory is needed.
        /// The function is inline so that a comptime-known `value` parameter will
        /// have a more optimal memset codegen in case it has a repeated byte pattern.
        pub inline fn appendNTimes(self: *Self, gpa: Allocator, value: T, n: usize) Allocator.Error!void {
            const old_len = self.items.len;
            try self.resize(gpa, try addOrOom(old_len, n));
            @memset(self.items[old_len..self.items.len], value);
        }

        /// Append a value to the list `n` times.
        ///
        /// Never invalidates element pointers.
        ///
        /// The function is inline so that a comptime-known `value` parameter will
        /// have better memset codegen in case it has a repeated byte pattern.
        ///
        /// Asserts that the list can hold the additional items.
        pub inline fn appendNTimesAssumeCapacity(self: *Self, value: T, n: usize) void {
            const new_len = self.items.len + n;
            assert(new_len <= self.capacity);
            @memset(self.items.ptr[self.items.len..new_len], value);
            self.items.len = new_len;
        }

        /// Append a value to the list `n` times.
        ///
        /// Never invalidates element pointers.
        ///
        /// The function is inline so that a comptime-known `value` parameter will
        /// have better memset codegen in case it has a repeated byte pattern.
        ///
        /// If the list lacks unused capacity for the additional items, returns
        /// `error.OutOfMemory`.
        pub inline fn appendNTimesBounded(self: *Self, value: T, n: usize) error{OutOfMemory}!void {
            const new_len = self.items.len + n;
            if (self.capacity < new_len) return error.OutOfMemory;
            @memset(self.items.ptr[self.items.len..new_len], value);
            self.items.len = new_len;
        }

        /// Adjust the list length to `new_len`.
        /// Additional elements contain the value `undefined`.
        /// Invalidates element pointers if additional memory is needed.
        pub fn resize(self: *Self, gpa: Allocator, new_len: usize) Allocator.Error!void {
            try self.ensureTotalCapacity(gpa, new_len);
            self.items.len = new_len;
        }

        /// Reduce allocated capacity to `new_len`.
        /// May invalidate element pointers.
        /// Asserts that the new length is less than or equal to the previous length.
        pub fn shrinkAndFree(self: *Self, gpa: Allocator, new_len: usize) void {
            self.shrinkAndFreePrecise(gpa, new_len) catch |e| switch (e) {
                error.OutOfMemory => {
                    // No problem, capacity is still correct then.
                    self.items.len = new_len;
                    return;
                },
            };
        }

        /// Reduce allocated capacity to `new_len`.
        /// May invalidate element pointers.
        /// Asserts that the new length is less than or equal to the previous length.
        /// If succeds capacity is guaranteed to be equal to the length.
        pub fn shrinkAndFreePrecise(self: *Self, gpa: Allocator, new_len: usize) Allocator.Error!void {
            assert(new_len <= self.items.len);

            if (@sizeOf(T) == 0) {
                self.items.len = new_len;
                return;
            }

            const old_memory = self.allocatedSlice();
            if (gpa.remap(old_memory, new_len)) |new_items| {
                self.capacity = new_items.len;
                self.items = new_items;
                return;
            }

            const new_memory = try gpa.alignedAlloc(T, alignment, new_len);

            @memcpy(new_memory, self.items[0..new_len]);
            gpa.free(old_memory);
            self.items = new_memory;
            self.capacity = new_memory.len;
        }

        /// Shrinks capacity to match length.
        /// May invalidate element pointers.
        /// If succeds it is safe to call toOwnedSliceAssert().
        pub fn shrinkToLen(self: *Self, gpa: Allocator) Allocator.Error!void {
            try self.shrinkAndFreePrecise(gpa, self.items.len);
        }

        /// Shrinks or expands capacity to match length + 1.
        /// May invalidate element pointers.
        /// If succeds it is safe to call toOwnedSliceSentinelAssert().
        pub fn shrinkToLenSentinel(self: *Self, gpa: Allocator) Allocator.Error!void {
            std.debug.assert(self.items.len <= self.capacity);
            const required_len = self.items.len + 1;
            switch (std.math.order(required_len, self.capacity)) {
                .eq => return,
                .gt => {
                    try self.ensureTotalCapacityPrecise(gpa, required_len);
                },
                .lt => {
                    self.items.len += 1;
                    defer self.items.len -= 1;
                    try self.shrinkToLen(gpa);
                },
            }
        }

        /// Reduce length to `new_len`.
        /// Invalidates pointers to elements `items[new_len..]`.
        /// Keeps capacity the same.
        /// Asserts that the new length is less than or equal to the previous length.
        pub fn shrinkRetainingCapacity(self: *Self, new_len: usize) void {
            assert(new_len <= self.items.len);
            @memset(self.items[new_len..], undefined);
            self.items.len = new_len;
        }

        /// Reduce length to 0.
        /// Invalidates all element pointers.
        pub fn clearRetainingCapacity(self: *Self) void {
            @memset(self.items, undefined);
            self.items.len = 0;
        }

        /// Invalidates all element pointers.
        pub fn clearAndFree(self: *Self, gpa: Allocator) void {
            gpa.free(self.allocatedSlice());
            self.items.len = 0;
            self.capacity = 0;
        }

        /// Modify the array so that it can hold at least `new_capacity` items.
        /// Implements super-linear growth to achieve amortized O(1) append operations.
        /// Invalidates element pointers if additional memory is needed.
        pub fn ensureTotalCapacity(self: *Self, gpa: Allocator, new_capacity: usize) Allocator.Error!void {
            if (self.capacity >= new_capacity) return;
            return self.ensureTotalCapacityPrecise(gpa, growCapacity(new_capacity));
        }

        /// If the current capacity is less than `new_capacity`, this function will
        /// modify the array so that it can hold exactly `new_capacity` items.
        /// Invalidates element pointers if additional memory is needed.
        pub fn ensureTotalCapacityPrecise(self: *Self, gpa: Allocator, new_capacity: usize) Allocator.Error!void {
            if (@sizeOf(T) == 0) {
                self.capacity = math.maxInt(usize);
                return;
            }

            if (self.capacity >= new_capacity) return;

            // Here we avoid copying allocated but unused bytes by
            // attempting a resize in place, and falling back to allocating
            // a new buffer and doing our own copy. With a realloc() call,
            // the allocator implementation would pointlessly copy our
            // extra capacity.
            const old_memory = self.allocatedSlice();
            if (gpa.remap(old_memory, new_capacity)) |new_memory| {
                self.items.ptr = new_memory.ptr;
                self.capacity = new_memory.len;
            } else {
                const new_memory = try gpa.alignedAlloc(T, alignment, new_capacity);
                @memcpy(new_memory[0..self.items.len], self.items);
                gpa.free(old_memory);
                self.items.ptr = new_memory.ptr;
                self.capacity = new_memory.len;
            }
        }

        /// Modify the array so that it can hold at least `additional_count` **more** items.
        /// Invalidates element pointers if additional memory is needed.
        pub fn ensureUnusedCapacity(
            self: *Self,
            gpa: Allocator,
            additional_count: usize,
        ) Allocator.Error!void {
            return self.ensureTotalCapacity(gpa, try addOrOom(self.items.len, additional_count));
        }

        /// Increases the array's length to match the full capacity that is already allocated.
        /// The new elements have `undefined` values.
        /// Never invalidates element pointers.
        pub fn expandToCapacity(self: *Self) void {
            self.items.len = self.capacity;
        }

        /// Increase length by 1, returning pointer to the new item.
        /// The returned element pointer becomes invalid when the list is resized.
        pub fn addOne(self: *Self, gpa: Allocator) Allocator.Error!*T {
            // This can never overflow because `self.items` can never occupy the whole address space
            const newlen = self.items.len + 1;
            try self.ensureTotalCapacity(gpa, newlen);
            return self.addOneAssumeCapacity();
        }

        /// Increase length by 1, returning pointer to the new item.
        ///
        /// Never invalidates element pointers.
        ///
        /// The returned element pointer becomes invalid when the list is resized.
        ///
        /// Asserts that the list can hold one additional item.
        pub fn addOneAssumeCapacity(self: *Self) *T {
            assert(self.items.len < self.capacity);

            self.items.len += 1;
            return &self.items[self.items.len - 1];
        }

        /// Increase length by 1, returning pointer to the new item.
        ///
        /// Never invalidates element pointers.
        ///
        /// The returned element pointer becomes invalid when the list is resized.
        ///
        /// If the list lacks unused capacity for the additional item, returns `error.OutOfMemory`.
        pub fn addOneBounded(self: *Self) error{OutOfMemory}!*T {
            if (self.capacity - self.items.len < 1) return error.OutOfMemory;
            return addOneAssumeCapacity(self);
        }

        /// Resize the array, adding `n` new elements, which have `undefined` values.
        /// The return value is an array pointing to the newly allocated elements.
        /// The returned pointer becomes invalid when the list is resized.
        pub fn addManyAsArray(self: *Self, gpa: Allocator, comptime n: usize) Allocator.Error!*[n]T {
            const prev_len = self.items.len;
            try self.resize(gpa, try addOrOom(self.items.len, n));
            return self.items[prev_len..][0..n];
        }

        /// Resize the array, adding `n` new elements, which have `undefined` values.
        ///
        /// The return value is an array pointing to the newly allocated elements.
        ///
        /// Never invalidates element pointers.
        ///
        /// The returned pointer becomes invalid when the list is resized.
        ///
        /// Asserts that the list can hold the additional items.
        pub fn addManyAsArrayAssumeCapacity(self: *Self, comptime n: usize) *[n]T {
            assert(self.items.len + n <= self.capacity);
            const prev_len = self.items.len;
            self.items.len += n;
            return self.items[prev_len..][0..n];
        }

        /// Resize the array, adding `n` new elements, which have `undefined` values.
        ///
        /// The return value is an array pointing to the newly allocated elements.
        ///
        /// Never invalidates element pointers.
        ///
        /// The returned pointer becomes invalid when the list is resized.
        ///
        /// If the list lacks unused capacity for the additional items, returns
        /// `error.OutOfMemory`.
        pub fn addManyAsArrayBounded(self: *Self, comptime n: usize) error{OutOfMemory}!*[n]T {
            if (self.capacity - self.items.len < n) return error.OutOfMemory;
            return addManyAsArrayAssumeCapacity(self, n);
        }

        /// Resize the array, adding `n` new elements, which have `undefined` values.
        /// The return value is a slice pointing to the newly allocated elements.
        /// The returned pointer becomes invalid when the list is resized.
        /// Resizes list if `self.capacity` is not large enough.
        pub fn addManyAsSlice(self: *Self, gpa: Allocator, n: usize) Allocator.Error![]T {
            const prev_len = self.items.len;
            try self.resize(gpa, try addOrOom(self.items.len, n));
            return self.items[prev_len..][0..n];
        }

        /// Resizes the array, adding `n` new elements, which have `undefined`
        /// values, returning a slice pointing to the newly allocated elements.
        ///
        /// Never invalidates element pointers. The returned pointer becomes
        /// invalid when the list is resized.
        ///
        /// Asserts that the list can hold the additional items.
        pub fn addManyAsSliceAssumeCapacity(self: *Self, n: usize) []T {
            assert(self.items.len + n <= self.capacity);
            const prev_len = self.items.len;
            self.items.len += n;
            return self.items[prev_len..][0..n];
        }

        /// Resizes the array, adding `n` new elements, which have `undefined`
        /// values, returning a slice pointing to the newly allocated elements.
        ///
        /// Never invalidates element pointers. The returned pointer becomes
        /// invalid when the list is resized.
        ///
        /// If the list lacks unused capacity for the additional items, returns
        /// `error.OutOfMemory`.
        pub fn addManyAsSliceBounded(self: *Self, n: usize) error{OutOfMemory}![]T {
            if (self.capacity - self.items.len < n) return error.OutOfMemory;
            return addManyAsSliceAssumeCapacity(self, n);
        }

        /// Remove and return the last element from the list.
        /// If the list is empty, returns `null`.
        /// Invalidates pointers to last element.
        pub fn pop(self: *Self) ?T {
            if (self.items.len == 0) return null;
            const val = self.items[self.items.len - 1];
            self.items[self.items.len - 1] = undefined;
            self.items.len -= 1;
            return val;
        }

        /// Returns a slice of all the items plus the extra capacity, whose memory
        /// contents are `undefined`.
        pub fn allocatedSlice(self: Self) Slice {
            return self.items.ptr[0..self.capacity];
        }

        /// Returns a slice of only the extra capacity after items.
        /// This can be useful for writing directly into an ArrayList.
        /// Note that such an operation must be followed up with a direct
        /// modification of `self.items.len`.
        pub fn unusedCapacitySlice(self: Self) []T {
            return self.allocatedSlice()[self.items.len..];
        }

        /// Return the last element from the list.
        /// Asserts that the list is not empty.
        pub fn getLast(self: Self) T {
            return self.items[self.items.len - 1];
        }

        /// Return the last element from the list, or
        /// return `null` if list is empty.
        pub fn getLastOrNull(self: Self) ?T {
            if (self.items.len == 0) return null;
            return self.getLast();
        }

        /// Called when memory growth is necessary. Returns a capacity larger than
        /// minimum that grows super-linearly.
        pub fn growCapacity(minimum: usize) usize {
            if (@sizeOf(T) == 0) return math.maxInt(usize);
            const init_capacity: comptime_int = @max(1, std.atomic.cache_line / @sizeOf(T));
            return minimum +| (minimum / 2 + init_capacity);
        }
    };
}

/// Integer addition returning `error.OutOfMemory` on overflow.
fn addOrOom(a: usize, b: usize) error{OutOfMemory}!usize {
    const result, const overflow = @addWithOverflow(a, b);
    if (overflow != 0) return error.OutOfMemory;
    return result;
}

test "init" {
    {
        var list = Managed(i32).init(testing.allocator);
        defer list.deinit();

        try testing.expect(list.items.len == 0);
        try testing.expect(list.capacity == 0);
    }

    {
        const list: ArrayList(i32) = .empty;

        try testing.expect(list.items.len == 0);
        try testing.expect(list.capacity == 0);
    }
}

test "initCapacity" {
    const a = testing.allocator;
    {
        var list = try Managed(i8).initCapacity(a, 200);
        defer list.deinit();
        try testing.expect(list.items.len == 0);
        try testing.expect(list.capacity >= 200);
    }
    {
        var list = try ArrayList(i8).initCapacity(a, 200);
        defer list.deinit(a);
        try testing.expect(list.items.len == 0);
        try testing.expect(list.capacity >= 200);
    }
}

test "clone" {
    const a = testing.allocator;
    {
        var array = Managed(i32).init(a);
        try array.append(-1);
        try array.append(3);
        try array.append(5);

        const cloned = try array.clone();
        defer cloned.deinit();

        try testing.expectEqualSlices(i32, array.items, cloned.items);
        try testing.expectEqual(array.allocator, cloned.allocator);
        try testing.expect(cloned.capacity >= array.capacity);

        array.deinit();

        try testing.expectEqual(@as(i32, -1), cloned.items[0]);
        try testing.expectEqual(@as(i32, 3), cloned.items[1]);
        try testing.expectEqual(@as(i32, 5), cloned.items[2]);
    }
    {
        var array: ArrayList(i32) = .empty;
        try array.append(a, -1);
        try array.append(a, 3);
        try array.append(a, 5);

        var cloned = try array.clone(a);
        defer cloned.deinit(a);

        try testing.expectEqualSlices(i32, array.items, cloned.items);
        try testing.expect(cloned.capacity >= array.capacity);

        array.deinit(a);

        try testing.expectEqual(@as(i32, -1), cloned.items[0]);
        try testing.expectEqual(@as(i32, 3), cloned.items[1]);
        try testing.expectEqual(@as(i32, 5), cloned.items[2]);
    }
}

test "basic" {
    const a = testing.allocator;
    {
        var list = Managed(i32).init(a);
        defer list.deinit();

        {
            var i: usize = 0;
            while (i < 10) : (i += 1) {
                list.append(@as(i32, @intCast(i + 1))) catch unreachable;
            }
        }

        {
            var i: usize = 0;
            while (i < 10) : (i += 1) {
                try testing.expect(list.items[i] == @as(i32, @intCast(i + 1)));
            }
        }

        for (list.items, 0..) |v, i| {
            try testing.expect(v == @as(i32, @intCast(i + 1)));
        }

        try testing.expect(list.pop() == 10);
        try testing.expect(list.items.len == 9);

        list.appendSlice(&[_]i32{ 1, 2, 3 }) catch unreachable;
        try testing.expect(list.items.len == 12);
        try testing.expect(list.pop() == 3);
        try testing.expect(list.pop() == 2);
        try testing.expect(list.pop() == 1);
        try testing.expect(list.items.len == 9);

        var unaligned: [3]i32 align(1) = [_]i32{ 4, 5, 6 };
        list.appendUnalignedSlice(&unaligned) catch unreachable;
        try testing.expect(list.items.len == 12);
        try testing.expect(list.pop() == 6);
        try testing.expect(list.pop() == 5);
        try testing.expect(list.pop() == 4);
        try testing.expect(list.items.len == 9);

        list.appendSlice(&[_]i32{}) catch unreachable;
        try testing.expect(list.items.len == 9);

        // can only set on indices < self.items.len
        list.items[7] = 33;
        list.items[8] = 42;

        try testing.expect(list.pop() == 42);
        try testing.expect(list.pop() == 33);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);

        {
            var i: usize = 0;
            while (i < 10) : (i += 1) {
                list.append(a, @as(i32, @intCast(i + 1))) catch unreachable;
            }
        }

        {
            var i: usize = 0;
            while (i < 10) : (i += 1) {
                try testing.expect(list.items[i] == @as(i32, @intCast(i + 1)));
            }
        }

        for (list.items, 0..) |v, i| {
            try testing.expect(v == @as(i32, @intCast(i + 1)));
        }

        try testing.expect(list.pop() == 10);
        try testing.expect(list.items.len == 9);

        list.appendSlice(a, &[_]i32{ 1, 2, 3 }) catch unreachable;
        try testing.expect(list.items.len == 12);
        try testing.expect(list.pop() == 3);
        try testing.expect(list.pop() == 2);
        try testing.expect(list.pop() == 1);
        try testing.expect(list.items.len == 9);

        var unaligned: [3]i32 align(1) = [_]i32{ 4, 5, 6 };
        list.appendUnalignedSlice(a, &unaligned) catch unreachable;
        try testing.expect(list.items.len == 12);
        try testing.expect(list.pop() == 6);
        try testing.expect(list.pop() == 5);
        try testing.expect(list.pop() == 4);
        try testing.expect(list.items.len == 9);

        list.appendSlice(a, &[_]i32{}) catch unreachable;
        try testing.expect(list.items.len == 9);

        // can only set on indices < self.items.len
        list.items[7] = 33;
        list.items[8] = 42;

        try testing.expect(list.pop() == 42);
        try testing.expect(list.pop() == 33);
    }
}

test "appendNTimes" {
    const a = testing.allocator;
    {
        var list = Managed(i32).init(a);
        defer list.deinit();

        try list.appendNTimes(2, 10);
        try testing.expectEqual(@as(usize, 10), list.items.len);
        for (list.items) |element| {
            try testing.expectEqual(@as(i32, 2), element);
        }
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);

        try list.appendNTimes(a, 2, 10);
        try testing.expectEqual(@as(usize, 10), list.items.len);
        for (list.items) |element| {
            try testing.expectEqual(@as(i32, 2), element);
        }
    }
}

test "appendNTimes with failing allocator" {
    const a = testing.failing_allocator;
    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try testing.expectError(error.OutOfMemory, list.appendNTimes(2, 10));
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try testing.expectError(error.OutOfMemory, list.appendNTimes(a, 2, 10));
    }
}

test "orderedRemove" {
    const a = testing.allocator;
    {
        var list = Managed(i32).init(a);
        defer list.deinit();

        try list.append(1);
        try list.append(2);
        try list.append(3);
        try list.append(4);
        try list.append(5);
        try list.append(6);
        try list.append(7);

        //remove from middle
        try testing.expectEqual(@as(i32, 4), list.orderedRemove(3));
        try testing.expectEqual(@as(i32, 5), list.items[3]);
        try testing.expectEqual(@as(usize, 6), list.items.len);

        //remove from end
        try testing.expectEqual(@as(i32, 7), list.orderedRemove(5));
        try testing.expectEqual(@as(usize, 5), list.items.len);

        //remove from front
        try testing.expectEqual(@as(i32, 1), list.orderedRemove(0));
        try testing.expectEqual(@as(i32, 2), list.items[0]);
        try testing.expectEqual(@as(usize, 4), list.items.len);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);

        try list.append(a, 1);
        try list.append(a, 2);
        try list.append(a, 3);
        try list.append(a, 4);
        try list.append(a, 5);
        try list.append(a, 6);
        try list.append(a, 7);

        //remove from middle
        try testing.expectEqual(@as(i32, 4), list.orderedRemove(3));
        try testing.expectEqual(@as(i32, 5), list.items[3]);
        try testing.expectEqual(@as(usize, 6), list.items.len);

        //remove from end
        try testing.expectEqual(@as(i32, 7), list.orderedRemove(5));
        try testing.expectEqual(@as(usize, 5), list.items.len);

        //remove from front
        try testing.expectEqual(@as(i32, 1), list.orderedRemove(0));
        try testing.expectEqual(@as(i32, 2), list.items[0]);
        try testing.expectEqual(@as(usize, 4), list.items.len);
    }
    {
        // remove last item
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.append(1);
        try testing.expectEqual(@as(i32, 1), list.orderedRemove(0));
        try testing.expectEqual(@as(usize, 0), list.items.len);
    }
    {
        // remove last item
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.append(a, 1);
        try testing.expectEqual(@as(i32, 1), list.orderedRemove(0));
        try testing.expectEqual(@as(usize, 0), list.items.len);
    }
}

test "swapRemove" {
    const a = testing.allocator;
    {
        var list = Managed(i32).init(a);
        defer list.deinit();

        try list.append(1);
        try list.append(2);
        try list.append(3);
        try list.append(4);
        try list.append(5);
        try list.append(6);
        try list.append(7);

        //remove from middle
        try testing.expect(list.swapRemove(3) == 4);
        try testing.expect(list.items[3] == 7);
        try testing.expect(list.items.len == 6);

        //remove from end
        try testing.expect(list.swapRemove(5) == 6);
        try testing.expect(list.items.len == 5);

        //remove from front
        try testing.expect(list.swapRemove(0) == 1);
        try testing.expect(list.items[0] == 5);
        try testing.expect(list.items.len == 4);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);

        try list.append(a, 1);
        try list.append(a, 2);
        try list.append(a, 3);
        try list.append(a, 4);
        try list.append(a, 5);
        try list.append(a, 6);
        try list.append(a, 7);

        //remove from middle
        try testing.expect(list.swapRemove(3) == 4);
        try testing.expect(list.items[3] == 7);
        try testing.expect(list.items.len == 6);

        //remove from end
        try testing.expect(list.swapRemove(5) == 6);
        try testing.expect(list.items.len == 5);

        //remove from front
        try testing.expect(list.swapRemove(0) == 1);
        try testing.expect(list.items[0] == 5);
        try testing.expect(list.items.len == 4);
    }
}

test "insert" {
    const a = testing.allocator;
    {
        var list = Managed(i32).init(a);
        defer list.deinit();

        try list.insert(0, 1);
        try list.append(2);
        try list.insert(2, 3);
        try list.insert(0, 5);
        try testing.expect(list.items[0] == 5);
        try testing.expect(list.items[1] == 1);
        try testing.expect(list.items[2] == 2);
        try testing.expect(list.items[3] == 3);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);

        try list.insert(a, 0, 1);
        try list.append(a, 2);
        try list.insert(a, 2, 3);
        try list.insert(a, 0, 5);
        try testing.expect(list.items[0] == 5);
        try testing.expect(list.items[1] == 1);
        try testing.expect(list.items[2] == 2);
        try testing.expect(list.items[3] == 3);
    }
    {
        var list: ArrayList(struct {}) = .empty;
        defer list.deinit(a);

        try list.insert(a, 0, .{});
        try list.append(a, .{});
        try testing.expect(list.items.len == 2);
    }
}

test "insertSlice" {
    const a = testing.allocator;
    {
        var list = Managed(i32).init(a);
        defer list.deinit();

        try list.append(1);
        try list.append(2);
        try list.append(3);
        try list.append(4);
        try list.insertSlice(1, &[_]i32{ 9, 8 });
        try testing.expect(list.items[0] == 1);
        try testing.expect(list.items[1] == 9);
        try testing.expect(list.items[2] == 8);
        try testing.expect(list.items[3] == 2);
        try testing.expect(list.items[4] == 3);
        try testing.expect(list.items[5] == 4);

        const items = [_]i32{1};
        try list.insertSlice(0, items[0..0]);
        try testing.expect(list.items.len == 6);
        try testing.expect(list.items[0] == 1);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);

        try list.append(a, 1);
        try list.append(a, 2);
        try list.append(a, 3);
        try list.append(a, 4);
        try list.insertSlice(a, 1, &[_]i32{ 9, 8 });
        try testing.expect(list.items[0] == 1);
        try testing.expect(list.items[1] == 9);
        try testing.expect(list.items[2] == 8);
        try testing.expect(list.items[3] == 2);
        try testing.expect(list.items[4] == 3);
        try testing.expect(list.items[5] == 4);

        const items = [_]i32{1};
        try list.insertSlice(a, 0, items[0..0]);
        try testing.expect(list.items.len == 6);
        try testing.expect(list.items[0] == 1);
    }
}

test "Managed.replaceRange" {
    const a = testing.allocator;

    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(1, 0, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 2, 3, 4, 5 }, list.items);
    }
    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(1, 1, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(
            i32,
            &[_]i32{ 1, 0, 0, 0, 3, 4, 5 },
            list.items,
        );
    }
    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(1, 2, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 4, 5 }, list.items);
    }
    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(1, 3, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 5 }, list.items);
    }
    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(1, 4, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0 }, list.items);
    }
}

test "Managed.replaceRangeAssumeCapacity" {
    const a = testing.allocator;

    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 0, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 2, 3, 4, 5 }, list.items);
    }
    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 1, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(
            i32,
            &[_]i32{ 1, 0, 0, 0, 3, 4, 5 },
            list.items,
        );
    }
    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 2, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 4, 5 }, list.items);
    }
    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 3, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 5 }, list.items);
    }
    {
        var list = Managed(i32).init(a);
        defer list.deinit();
        try list.appendSlice(&[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 4, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0 }, list.items);
    }
}

test "ArrayList.replaceRange" {
    const a = testing.allocator;

    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(a, 1, 0, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 2, 3, 4, 5 }, list.items);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(a, 1, 1, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(
            i32,
            &[_]i32{ 1, 0, 0, 0, 3, 4, 5 },
            list.items,
        );
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(a, 1, 2, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 4, 5 }, list.items);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(a, 1, 3, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 5 }, list.items);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        try list.replaceRange(a, 1, 4, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0 }, list.items);
    }
}

test "ArrayList.replaceRangeAssumeCapacity" {
    const a = testing.allocator;

    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 0, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 2, 3, 4, 5 }, list.items);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 1, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(
            i32,
            &[_]i32{ 1, 0, 0, 0, 3, 4, 5 },
            list.items,
        );
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 2, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 4, 5 }, list.items);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 3, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0, 5 }, list.items);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);
        try list.appendSlice(a, &[_]i32{ 1, 2, 3, 4, 5 });

        list.replaceRangeAssumeCapacity(1, 4, &[_]i32{ 0, 0, 0 });

        try testing.expectEqualSlices(i32, &[_]i32{ 1, 0, 0, 0 }, list.items);
    }
}

const Item = struct {
    integer: i32,
    sub_items: Managed(Item),
};

const ItemUnmanaged = struct {
    integer: i32,
    sub_items: ArrayList(ItemUnmanaged),
};

test "Managed(T) of struct T" {
    const a = std.testing.allocator;
    {
        var root = Item{ .integer = 1, .sub_items = .init(a) };
        defer root.sub_items.deinit();
        try root.sub_items.append(Item{ .integer = 42, .sub_items = .init(a) });
        try testing.expect(root.sub_items.items[0].integer == 42);
    }
    {
        var root = ItemUnmanaged{ .integer = 1, .sub_items = .empty };
        defer root.sub_items.deinit(a);
        try root.sub_items.append(a, ItemUnmanaged{ .integer = 42, .sub_items = .empty });
        try testing.expect(root.sub_items.items[0].integer == 42);
    }
}

test "shrink still sets length when resizing is disabled" {
    var failing_allocator = testing.FailingAllocator.init(testing.allocator, .{ .resize_fail_index = 0 });
    const a = failing_allocator.allocator();

    {
        var list = Managed(i32).init(a);
        defer list.deinit();

        try list.append(1);
        try list.append(2);
        try list.append(3);

        list.shrinkAndFree(1);
        try testing.expect(list.items.len == 1);
    }
    {
        var list: ArrayList(i32) = .empty;
        defer list.deinit(a);

        try list.append(a, 1);
        try list.append(a, 2);
        try list.append(a, 3);

        list.shrinkAndFree(a, 1);
        try testing.expect(list.items.len == 1);
    }
}

test "shrinkAndFree with a copy" {
    var failing_allocator = testing.FailingAllocator.init(testing.allocator, .{ .resize_fail_index = 0 });
    const a = failing_allocator.allocator();

    var list = Managed(i32).init(a);
    defer list.deinit();

    try list.appendNTimes(3, 16);
    list.shrinkAndFree(4);
    try testing.expect(mem.eql(i32, list.items, &.{ 3, 3, 3, 3 }));
}

test "shrinkAndFreePrecise without resize succeeds" {
    var failing_allocator = testing.FailingAllocator.init(testing.allocator, .{ .resize_fail_index = 0 });
    const a = failing_allocator.allocator();

    var list: Aligned(i32, null) = .empty;
    defer list.deinit(a);

    try list.appendNTimes(a, 3, 16);
    try list.shrinkAndFreePrecise(a, 4);
    try testing.expectEqualSlices(i32, &.{ 3, 3, 3, 3 }, list.items);
    try testing.expectEqual(list.items.len, list.capacity);
}

test "shrinkAndFreePrecise without resize and no copy failes" {
    var failing_allocator = testing.FailingAllocator.init(testing.allocator, .{ .resize_fail_index = 0, .fail_index = 1 });
    const a = failing_allocator.allocator();

    var list: Aligned(i32, null) = .empty;
    defer list.deinit(a);

    try list.appendNTimes(a, 3, 16);
    try std.testing.expectError(error.OutOfMemory, list.shrinkAndFreePrecise(a, 4));
}

test "addManyAsArray" {
    const a = std.testing.allocator;
    {
        var list = Managed(u8).init(a);
        defer list.deinit();

        (try list.addManyAsArray(4)).* = "aoeu".*;
        try list.ensureTotalCapacity(8);
        list.addManyAsArrayAssumeCapacity(4).* = "asdf".*;

        try testing.expectEqualSlices(u8, list.items, "aoeuasdf");
    }
    {
        var list: ArrayList(u8) = .empty;
        defer list.deinit(a);

        (try list.addManyAsArray(a, 4)).* = "aoeu".*;
        try list.ensureTotalCapacity(a, 8);
        list.addManyAsArrayAssumeCapacity(4).* = "asdf".*;

        try testing.expectEqualSlices(u8, list.items, "aoeuasdf");
    }
}

test "growing memory preserves contents" {
    // Shrink the list after every insertion to ensure that a memory growth
    // will be triggered in the next operation.
    const a = std.testing.allocator;
    {
        var list = Managed(u8).init(a);
        defer list.deinit();

        (try list.addManyAsArray(4)).* = "abcd".*;
        list.shrinkAndFree(4);

        try list.appendSlice("efgh");
        try testing.expectEqualSlices(u8, list.items, "abcdefgh");
        list.shrinkAndFree(8);

        try list.insertSlice(4, "ijkl");
        try testing.expectEqualSlices(u8, list.items, "abcdijklefgh");
    }
    {
        var list: ArrayList(u8) = .empty;
        defer list.deinit(a);

        (try list.addManyAsArray(a, 4)).* = "abcd".*;
        list.shrinkAndFree(a, 4);

        try list.appendSlice(a, "efgh");
        try testing.expectEqualSlices(u8, list.items, "abcdefgh");
        list.shrinkAndFree(a, 8);

        try list.insertSlice(a, 4, "ijkl");
        try testing.expectEqualSlices(u8, list.items, "abcdijklefgh");
    }
}

test "fromOwnedSlice" {
    const a = testing.allocator;
    {
        var orig_list = Managed(u8).init(a);
        defer orig_list.deinit();
        try orig_list.appendSlice("foobar");

        const slice = try orig_list.toOwnedSlice();
        var list = Managed(u8).fromOwnedSlice(a, slice);
        defer list.deinit();
        try testing.expectEqualStrings(list.items, "foobar");
    }
    {
        var list = Managed(u8).init(a);
        defer list.deinit();
        try list.appendSlice("foobar");

        const slice = try list.toOwnedSlice();
        var unmanaged = ArrayList(u8).fromOwnedSlice(slice);
        defer unmanaged.deinit(a);
        try testing.expectEqualStrings(unmanaged.items, "foobar");
    }
}

test "fromOwnedSliceSentinel" {
    const a = testing.allocator;
    {
        var orig_list = Managed(u8).init(a);
        defer orig_list.deinit();
        try orig_list.appendSlice("foobar");

        const sentinel_slice = try orig_list.toOwnedSliceSentinel(0);
        var list = Managed(u8).fromOwnedSliceSentinel(a, 0, sentinel_slice);
        defer list.deinit();
        try testing.expectEqualStrings(list.items, "foobar");
    }
    {
        var list = Managed(u8).init(a);
        defer list.deinit();
        try list.appendSlice("foobar");

        const sentinel_slice = try list.toOwnedSliceSentinel(0);
        var unmanaged = ArrayList(u8).fromOwnedSliceSentinel(0, sentinel_slice);
        defer unmanaged.deinit(a);
        try testing.expectEqualStrings(unmanaged.items, "foobar");
    }
}

test "toOwnedSliceSentinel" {
    const a = testing.allocator;
    {
        var list = Managed(u8).init(a);
        defer list.deinit();

        try list.appendSlice("foobar");

        const result = try list.toOwnedSliceSentinel(0);
        defer a.free(result);
        try testing.expectEqualStrings(result, mem.sliceTo(result.ptr, 0));
    }
    {
        var list: ArrayList(u8) = .empty;
        defer list.deinit(a);

        try list.appendSlice(a, "foobar");

        const result = try list.toOwnedSliceSentinel(a, 0);
        defer a.free(result);
        try testing.expectEqualStrings(result, mem.sliceTo(result.ptr, 0));
    }
}

test "toOwnedSliceAssert" {
    var failing_allocator: testing.FailingAllocator = .init(testing.allocator, .{
        .fail_index = 2,
    });
    const a = failing_allocator.allocator();

    var list: Aligned(u8, null) = try .initCapacity(a, 6); // first alloc
    list.appendSliceAssumeCapacity(&.{ 1, 2, 3 });

    try list.shrinkToLen(a); // first resize
    try std.testing.expectEqual(list.items.len, list.capacity);
    try list.shrinkToLen(a); // no alloc or resize

    const slice = list.toOwnedSliceAssert();
    defer a.free(slice);

    try std.testing.expectEqual(Aligned(u8, null).empty, list);
    try std.testing.expectEqualSlices(u8, &.{ 1, 2, 3 }, slice);
}

test "toOwnedSliceSentinelAssert" {
    const a = testing.allocator;

    var list: Aligned(u8, null) = try .initCapacity(a, 6);
    list.appendSliceAssumeCapacity(&.{ 1, 2, 3 });

    // shrinkToLenSentinel shrinks array
    try list.shrinkToLenSentinel(a);

    // shrinkToLenSentinel expands array
    try list.shrinkToLen(a);
    try list.shrinkToLenSentinel(a);

    const slice = list.toOwnedSliceSentinelAssert(10);
    defer a.free(slice);

    try std.testing.expectEqualSentinel(u8, 10, &.{ 1, 2, 3 }, slice);
}

test "accepts unaligned slices" {
    const a = testing.allocator;
    {
        var list = AlignedManaged(u8, .@"8").init(a);
        defer list.deinit();

        try list.appendSlice(&.{ 0, 1, 2, 3 });
        try list.insertSlice(2, &.{ 4, 5, 6, 7 });
        try list.replaceRange(1, 3, &.{ 8, 9 });

        try testing.expectEqualSlices(u8, list.items, &.{ 0, 8, 9, 6, 7, 2, 3 });
    }
    {
        var list: Aligned(u8, .@"8") = .empty;
        defer list.deinit(a);

        try list.appendSlice(a, &.{ 0, 1, 2, 3 });
        try list.insertSlice(a, 2, &.{ 4, 5, 6, 7 });
        try list.replaceRange(a, 1, 3, &.{ 8, 9 });

        try testing.expectEqualSlices(u8, list.items, &.{ 0, 8, 9, 6, 7, 2, 3 });
    }
}

test "Managed(u0)" {
    // An Managed on zero-sized types should not need to allocate
    const a = testing.failing_allocator;

    var list = Managed(u0).init(a);
    defer list.deinit();

    try list.append(0);
    try list.append(0);
    try list.append(0);
    try testing.expectEqual(list.items.len, 3);

    var count: usize = 0;
    for (list.items) |x| {
        try testing.expectEqual(x, 0);
        count += 1;
    }
    try testing.expectEqual(count, 3);
}

test "Managed(?u32).pop()" {
    const a = testing.allocator;

    var list = Managed(?u32).init(a);
    defer list.deinit();

    try list.append(null);
    try list.append(1);
    try list.append(2);
    try testing.expectEqual(list.items.len, 3);

    try testing.expect(list.pop().? == @as(u32, 2));
    try testing.expect(list.pop().? == @as(u32, 1));
    try testing.expect(list.pop().? == null);
    try testing.expect(list.pop() == null);
}

test "Managed(u32).getLast()" {
    const a = testing.allocator;

    var list = Managed(u32).init(a);
    defer list.deinit();

    try list.append(2);
    const const_list = list;
    try testing.expectEqual(const_list.getLast(), 2);
}

test "Managed(u32).getLastOrNull()" {
    const a = testing.allocator;

    var list = Managed(u32).init(a);
    defer list.deinit();

    try testing.expectEqual(list.getLastOrNull(), null);

    try list.append(2);
    const const_list = list;
    try testing.expectEqual(const_list.getLastOrNull().?, 2);
}

test "return OutOfMemory when capacity would exceed maximum usize integer value" {
    const a = testing.allocator;
    const new_item: u32 = 42;
    const items = &.{ 42, 43 };

    {
        var list: ArrayList(u32) = .{
            .items = undefined,
            .capacity = math.maxInt(usize) - 1,
        };
        list.items.len = math.maxInt(usize) - 1;

        try testing.expectError(error.OutOfMemory, list.appendSlice(a, items));
        try testing.expectError(error.OutOfMemory, list.appendNTimes(a, new_item, 2));
        try testing.expectError(error.OutOfMemory, list.appendUnalignedSlice(a, &.{ new_item, new_item }));
        try testing.expectError(error.OutOfMemory, list.addManyAt(a, 0, 2));
        try testing.expectError(error.OutOfMemory, list.addManyAsArray(a, 2));
        try testing.expectError(error.OutOfMemory, list.addManyAsSlice(a, 2));
        try testing.expectError(error.OutOfMemory, list.insertSlice(a, 0, items));
        try testing.expectError(error.OutOfMemory, list.ensureUnusedCapacity(a, 2));
    }

    {
        var list: Managed(u32) = .{
            .items = undefined,
            .capacity = math.maxInt(usize) - 1,
            .allocator = a,
        };
        list.items.len = math.maxInt(usize) - 1;

        try testing.expectError(error.OutOfMemory, list.appendSlice(items));
        try testing.expectError(error.OutOfMemory, list.appendNTimes(new_item, 2));
        try testing.expectError(error.OutOfMemory, list.appendUnalignedSlice(&.{ new_item, new_item }));
        try testing.expectError(error.OutOfMemory, list.addManyAt(0, 2));
        try testing.expectError(error.OutOfMemory, list.addManyAsArray(2));
        try testing.expectError(error.OutOfMemory, list.addManyAsSlice(2));
        try testing.expectError(error.OutOfMemory, list.insertSlice(0, items));
        try testing.expectError(error.OutOfMemory, list.ensureUnusedCapacity(2));
    }
}

test "orderedRemoveMany" {
    const gpa = testing.allocator;

    var list: Aligned(usize, null) = .empty;
    defer list.deinit(gpa);

    for (0..10) |n| {
        try list.append(gpa, n);
    }

    list.orderedRemoveMany(&.{ 1, 5, 5, 7, 9 });
    try testing.expectEqualSlices(usize, &.{ 0, 2, 3, 4, 6, 8 }, list.items);

    list.orderedRemoveMany(&.{0});
    try testing.expectEqualSlices(usize, &.{ 2, 3, 4, 6, 8 }, list.items);

    list.orderedRemoveMany(&.{});
    try testing.expectEqualSlices(usize, &.{ 2, 3, 4, 6, 8 }, list.items);

    list.orderedRemoveMany(&.{ 1, 2, 3, 4 });
    try testing.expectEqualSlices(usize, &.{2}, list.items);

    list.orderedRemoveMany(&.{0});
    try testing.expectEqualSlices(usize, &.{}, list.items);
}

test "insertSlice*" {
    var buf: [10]u8 = undefined;
    var list: ArrayList(u8) = .initBuffer(&buf);

    list.appendSliceAssumeCapacity("abcd");

    list.insertSliceAssumeCapacity(2, "ef");
    try testing.expectEqualStrings("abefcd", list.items);

    try list.insertSliceBounded(4, "gh");
    try testing.expectEqualStrings("abefghcd", list.items);

    try testing.expectError(error.OutOfMemory, list.insertSliceBounded(6, "ijkl"));
    try testing.expectEqualStrings("abefghcd", list.items); // ensure no elements were changed before the return of error.OutOfMemory

    list.insertSliceAssumeCapacity(6, "ij");
    try testing.expectEqualStrings("abefghijcd", list.items);
}



---
File: /std/ascii.zig
---

//! The 7-bit [ASCII](https://en.wikipedia.org/wiki/ASCII) character encoding standard.
//!
//! This is not to be confused with the 8-bit [extended ASCII](https://en.wikipedia.org/wiki/Extended_ASCII) character encoding.
//!
//! Even though this module concerns itself with 7-bit ASCII,
//! functions use `u8` as the type instead of `u7` for convenience and compatibility.
//! Characters outside of the 7-bit range are gracefully handled (e.g. by returning `false`).
//!
//! See also: https://en.wikipedia.org/wiki/ASCII#Character_set

const std = @import("std");

pub const lowercase = "abcdefghijklmnopqrstuvwxyz";
pub const uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
pub const letters = lowercase ++ uppercase;

/// The C0 control codes of the ASCII encoding.
///
/// See also: https://en.wikipedia.org/wiki/C0_and_C1_control_codes and `isControl`
pub const control_code = struct {
    /// Null.
    pub const nul = 0x00;
    /// Start of Heading.
    pub const soh = 0x01;
    /// Start of Text.
    pub const stx = 0x02;
    /// End of Text.
    pub const etx = 0x03;
    /// End of Transmission.
    pub const eot = 0x04;
    /// Enquiry.
    pub const enq = 0x05;
    /// Acknowledge.
    pub const ack = 0x06;
    /// Bell, Alert.
    pub const bel = 0x07;
    /// Backspace.
    pub const bs = 0x08;
    /// Horizontal Tab, Tab ('\t').
    pub const ht = 0x09;
    /// Line Feed, Newline ('\n').
    pub const lf = 0x0A;
    /// Vertical Tab.
    pub const vt = 0x0B;
    /// Form Feed.
    pub const ff = 0x0C;
    /// Carriage Return ('\r').
    pub const cr = 0x0D;
    /// Shift Out.
    pub const so = 0x0E;
    /// Shift In.
    pub const si = 0x0F;
    /// Data Link Escape.
    pub const dle = 0x10;
    /// Device Control One (XON).
    pub const dc1 = 0x11;
    /// Device Control Two.
    pub const dc2 = 0x12;
    /// Device Control Three (XOFF).
    pub const dc3 = 0x13;
    /// Device Control Four.
    pub const dc4 = 0x14;
    /// Negative Acknowledge.
    pub const nak = 0x15;
    /// Synchronous Idle.
    pub const syn = 0x16;
    /// End of Transmission Block
    pub const etb = 0x17;
    /// Cancel.
    pub const can = 0x18;
    /// End of Medium.
    pub const em = 0x19;
    /// Substitute.
    pub const sub = 0x1A;
    /// Escape.
    pub const esc = 0x1B;
    /// File Separator.
    pub const fs = 0x1C;
    /// Group Separator.
    pub const gs = 0x1D;
    /// Record Separator.
    pub const rs = 0x1E;
    /// Unit Separator.
    pub const us = 0x1F;

    /// Delete.
    pub const del = 0x7F;

    /// An alias to `dc1`.
    pub const xon = dc1;
    /// An alias to `dc3`.
    pub const xoff = dc3;
};

/// Returns whether the character is alphanumeric: A-Z, a-z, or 0-9.
pub fn isAlphanumeric(c: u8) bool {
    return switch (c) {
        '0'...'9', 'A'...'Z', 'a'...'z' => true,
        else => false,
    };
}

/// Returns whether the character is alphabetic: A-Z or a-z.
pub fn isAlphabetic(c: u8) bool {
    return switch (c) {
        'A'...'Z', 'a'...'z' => true,
        else => false,
    };
}

/// Returns whether the character is a control character.
///
/// See also: `control_code`
pub fn isControl(c: u8) bool {
    return c <= control_code.us or c == control_code.del;
}

/// Returns whether the character is a digit.
pub fn isDigit(c: u8) bool {
    return switch (c) {
        '0'...'9' => true,
        else => false,
    };
}

/// Returns whether the character is a lowercase letter.
pub fn isLower(c: u8) bool {
    return switch (c) {
        'a'...'z' => true,
        else => false,
    };
}

/// Returns whether the character is printable and has some graphical representation,
/// including the space character.
pub fn isPrint(c: u8) bool {
    return isAscii(c) and !isControl(c);
}

/// Returns whether the character has some graphical representation,
pub fn isGraphical(c: u8) bool {
    return isPrint(c) and c != ' ';
}

/// Returns whether the character is a punctuation character.
pub fn isPunctuation(c: u8) bool {
    return isGraphical(c) and !isAlphanumeric(c);
}

/// Returns whether this character is included in `whitespace`.
pub fn isWhitespace(c: u8) bool {
    return switch (c) {
        ' ', '\t'...'\r' => true,
        else => false,
    };
}

/// Whitespace for general use.
/// This may be used with e.g. `std.mem.trim` to trim whitespace.
///
/// See also: `isWhitespace`
pub const whitespace = [_]u8{ ' ', '\t', '\n', '\r', control_code.vt, control_code.ff };

test whitespace {
    for (whitespace) |char| try std.testing.expect(isWhitespace(char));

    var i: u8 = 0;
    while (isAscii(i)) : (i += 1) {
        if (isWhitespace(i)) try std.testing.expect(std.mem.findScalar(u8, &whitespace, i) != null);
    }
}

/// Returns whether the character is an uppercase letter.
pub fn isUpper(c: u8) bool {
    return switch (c) {
        'A'...'Z' => true,
        else => false,
    };
}

/// Returns whether the character is a hexadecimal digit: A-F, a-f, or 0-9.
pub fn isHex(c: u8) bool {
    return switch (c) {
        '0'...'9', 'A'...'F', 'a'...'f' => true,
        else => false,
    };
}

/// Returns whether the character is a 7-bit ASCII character.
pub fn isAscii(c: u8) bool {
    return c < 128;
}

/// Uppercases the character and returns it as-is if already uppercase or not a letter.
pub fn toUpper(c: u8) u8 {
    const mask = @as(u8, @intFromBool(isLower(c))) << 5;
    return c ^ mask;
}

/// Lowercases the character and returns it as-is if already lowercase or not a letter.
pub fn toLower(c: u8) u8 {
    const mask = @as(u8, @intFromBool(isUpper(c))) << 5;
    return c | mask;
}

test "ASCII character classes" {
    const testing = std.testing;

    try testing.expect(!isControl('a'));
    try testing.expect(!isControl('z'));
    try testing.expect(!isControl(' '));
    try testing.expect(isControl(control_code.nul));
    try testing.expect(isControl(control_code.ff));
    try testing.expect(isControl(control_code.us));
    try testing.expect(isControl(control_code.del));
    try testing.expect(!isControl(0x80));
    try testing.expect(!isControl(0xff));

    try testing.expect('C' == toUpper('c'));
    try testing.expect(':' == toUpper(':'));
    try testing.expect('\xab' == toUpper('\xab'));
    try testing.expect(!isUpper('z'));
    try testing.expect(!isUpper(0x80));
    try testing.expect(!isUpper(0xff));

    try testing.expect('c' == toLower('C'));
    try testing.expect(':' == toLower(':'));
    try testing.expect('\xab' == toLower('\xab'));
    try testing.expect(!isLower('Z'));
    try testing.expect(!isLower(0x80));
    try testing.expect(!isLower(0xff));

    try testing.expect(isAlphanumeric('Z'));
    try testing.expect(isAlphanumeric('z'));
    try testing.expect(isAlphanumeric('5'));
    try testing.expect(isAlphanumeric('a'));
    try testing.expect(!isAlphanumeric('!'));
    try testing.expect(!isAlphanumeric(0x80));
    try testing.expect(!isAlphanumeric(0xff));

    try testing.expect(!isAlphabetic('5'));
    try testing.expect(isAlphabetic('c'));
    try testing.expect(!isAlphabetic('@'));
    try testing.expect(isAlphabetic('Z'));
    try testing.expect(!isAlphabetic(0x80));
    try testing.expect(!isAlphabetic(0xff));

    try testing.expect(isWhitespace(' '));
    try testing.expect(isWhitespace('\t'));
    try testing.expect(isWhitespace('\r'));
    try testing.expect(isWhitespace('\n'));
    try testing.expect(isWhitespace(control_code.ff));
    try testing.expect(!isWhitespace('.'));
    try testing.expect(!isWhitespace(control_code.us));
    try testing.expect(!isWhitespace(0x80));
    try testing.expect(!isWhitespace(0xff));

    try testing.expect(!isHex('g'));
    try testing.expect(isHex('b'));
    try testing.expect(isHex('F'));
    try testing.expect(isHex('9'));
    try testing.expect(!isHex(0x80));
    try testing.expect(!isHex(0xff));

    try testing.expect(!isDigit('~'));
    try testing.expect(isDigit('0'));
    try testing.expect(isDigit('9'));
    try testing.expect(!isDigit(0x80));
    try testing.expect(!isDigit(0xff));

    try testing.expect(isPrint(' '));
    try testing.expect(isPrint('@'));
    try testing.expect(isPrint('~'));
    try testing.expect(!isPrint(control_code.esc));
    try testing.expect(!isPrint(0x80));
    try testing.expect(!isPrint(0xff));

    try testing.expect(isGraphical('@'));
    try testing.expect(isGraphical('!'));
    try testing.expect(!isGraphical(' '));

    try testing.expect(isPunctuation('@'));
    try testing.expect(isPunctuation('!'));
    try testing.expect(isPunctuation(';'));
    try testing.expect(isPunctuation(','));
    try testing.expect(!isPunctuation('A'));
    try testing.expect(!isPunctuation('8'));
}

/// Writes a lower case copy of `ascii_string` to `output`.
/// Asserts `output.len >= ascii_string.len`.
pub fn lowerString(output: []u8, ascii_string: []const u8) []u8 {
    std.debug.assert(output.len >= ascii_string.len);
    for (ascii_string, 0..) |c, i| {
        output[i] = toLower(c);
    }
    return output[0..ascii_string.len];
}

test lowerString {
    var buf: [1024]u8 = undefined;
    const result = lowerString(&buf, "aBcDeFgHiJkLmNOPqrst0234+💩!");
    try std.testing.expectEqualStrings("abcdefghijklmnopqrst0234+💩!", result);
}

/// Allocates a lower case copy of `ascii_string`.
/// Caller owns returned string and must free with `allocator`.
pub fn allocLowerString(allocator: std.mem.Allocator, ascii_string: []const u8) ![]u8 {
    const result = try allocator.alloc(u8, ascii_string.len);
    return lowerString(result, ascii_string);
}

test allocLowerString {
    const result = try allocLowerString(std.testing.allocator, "aBcDeFgHiJkLmNOPqrst0234+💩!");
    defer std.testing.allocator.free(result);
    try std.testing.expectEqualStrings("abcdefghijklmnopqrst0234+💩!", result);
}

/// Writes an upper case copy of `ascii_string` to `output`.
/// Asserts `output.len >= ascii_string.len`.
pub fn upperString(output: []u8, ascii_string: []const u8) []u8 {
    std.debug.assert(output.len >= ascii_string.len);
    for (ascii_string, 0..) |c, i| {
        output[i] = toUpper(c);
    }
    return output[0..ascii_string.len];
}

test upperString {
    var buf: [1024]u8 = undefined;
    const result = upperString(&buf, "aBcDeFgHiJkLmNOPqrst0234+💩!");
    try std.testing.expectEqualStrings("ABCDEFGHIJKLMNOPQRST0234+💩!", result);
}

/// Allocates an upper case copy of `ascii_string`.
/// Caller owns returned string and must free with `allocator`.
pub fn allocUpperString(allocator: std.mem.Allocator, ascii_string: []const u8) ![]u8 {
    const result = try allocator.alloc(u8, ascii_string.len);
    return upperString(result, ascii_string);
}

test allocUpperString {
    const result = try allocUpperString(std.testing.allocator, "aBcDeFgHiJkLmNOPqrst0234+💩!");
    defer std.testing.allocator.free(result);
    try std.testing.expectEqualStrings("ABCDEFGHIJKLMNOPQRST0234+💩!", result);
}

/// Compares strings `a` and `b` case-insensitively and returns whether they are equal.
pub fn eqlIgnoreCase(a: []const u8, b: []const u8) bool {
    if (a.len != b.len) return false;
    for (a, 0..) |a_c, i| {
        if (toLower(a_c) != toLower(b[i])) return false;
    }
    return true;
}

test eqlIgnoreCase {
    try std.testing.expect(eqlIgnoreCase("HEl💩Lo!", "hel💩lo!"));
    try std.testing.expect(!eqlIgnoreCase("hElLo!", "hello! "));
    try std.testing.expect(!eqlIgnoreCase("hElLo!", "helro!"));
}

pub fn startsWithIgnoreCase(haystack: []const u8, needle: []const u8) bool {
    return if (needle.len > haystack.len) false else eqlIgnoreCase(haystack[0..needle.len], needle);
}

test startsWithIgnoreCase {
    try std.testing.expect(startsWithIgnoreCase("boB", "Bo"));
    try std.testing.expect(!startsWithIgnoreCase("Needle in hAyStAcK", "haystack"));
}

pub fn endsWithIgnoreCase(haystack: []const u8, needle: []const u8) bool {
    return if (needle.len > haystack.len) false else eqlIgnoreCase(haystack[haystack.len - needle.len ..], needle);
}

test endsWithIgnoreCase {
    try std.testing.expect(endsWithIgnoreCase("Needle in HaYsTaCk", "haystack"));
    try std.testing.expect(!endsWithIgnoreCase("BoB", "Bo"));
}

/// Deprecated in favor of `findIgnoreCase`.
pub const indexOfIgnoreCase = findIgnoreCase;

/// Finds `needle` in `haystack`, ignoring case, starting at index 0.
pub fn findIgnoreCase(haystack: []const u8, needle: []const u8) ?usize {
    return findIgnoreCasePos(haystack, 0, needle);
}

/// Deprecated in favor of `findIgnoreCasePos`.
pub const indexOfIgnoreCasePos = findIgnoreCasePos;

/// Finds `needle` in `haystack`, ignoring case, starting at `start_index`.
/// Uses Boyer-Moore-Horspool algorithm on large inputs; `findIgnoreCasePosLinear` on small inputs.
pub fn findIgnoreCasePos(haystack: []const u8, start_index: usize, needle: []const u8) ?usize {
    if (needle.len > haystack.len) return null;
    if (needle.len == 0) return start_index;

    if (haystack.len < 52 or needle.len <= 4)
        return findIgnoreCasePosLinear(haystack, start_index, needle);

    var skip_table: [256]usize = undefined;
    boyerMooreHorspoolPreprocessIgnoreCase(needle, skip_table[0..]);

    var i: usize = start_index;
    while (i <= haystack.len - needle.len) {
        if (eqlIgnoreCase(haystack[i .. i + needle.len], needle)) return i;
        i += skip_table[toLower(haystack[i + needle.len - 1])];
    }

    return null;
}

/// Deprecated in favor of `findIgnoreCaseLinear`.
pub const indexOfIgnoreCasePosLinear = findIgnoreCasePosLinear;

/// Consider using `findIgnoreCasePos` instead of this, which will automatically use a
/// more sophisticated algorithm on larger inputs.
pub fn findIgnoreCasePosLinear(haystack: []const u8, start_index: usize, needle: []const u8) ?usize {
    var i: usize = start_index;
    const end = haystack.len - needle.len;
    while (i <= end) : (i += 1) {
        if (eqlIgnoreCase(haystack[i .. i + needle.len], needle)) return i;
    }
    return null;
}

fn boyerMooreHorspoolPreprocessIgnoreCase(pattern: []const u8, table: *[256]usize) void {
    for (table) |*c| {
        c.* = pattern.len;
    }

    var i: usize = 0;
    // The last item is intentionally ignored and the skip size will be pattern.len.
    // This is the standard way Boyer-Moore-Horspool is implemented.
    while (i < pattern.len - 1) : (i += 1) {
        table[toLower(pattern[i])] = pattern.len - 1 - i;
    }
}

test findIgnoreCase {
    try std.testing.expect(findIgnoreCase("one Two Three Four", "foUr").? == 14);
    try std.testing.expect(findIgnoreCase("one two three FouR", "gOur") == null);
    try std.testing.expect(findIgnoreCase("foO", "Foo").? == 0);
    try std.testing.expect(findIgnoreCase("foo", "fool") == null);
    try std.testing.expect(findIgnoreCase("FOO foo", "fOo").? == 0);

    try std.testing.expect(findIgnoreCase("one two three four five six seven eight nine ten eleven", "ThReE fOUr").? == 8);
    try std.testing.expect(findIgnoreCase("one two three four five six seven eight nine ten eleven", "Two tWo") == null);
}

/// Returns the lexicographical order of two slices. O(n).
pub fn orderIgnoreCase(lhs: []const u8, rhs: []const u8) std.math.Order {
    if (lhs.ptr != rhs.ptr) {
        const n = @min(lhs.len, rhs.len);
        var i: usize = 0;
        while (i < n) : (i += 1) {
            switch (std.math.order(toLower(lhs[i]), toLower(rhs[i]))) {
                .eq => continue,
                .lt => return .lt,
                .gt => return .gt,
            }
        }
    }
    return std.math.order(lhs.len, rhs.len);
}

/// Returns the lexicographical order of two many-item pointers with NUL-termination. O(n).
pub fn orderIgnoreCaseZ(lhs: [*:0]const u8, rhs: [*:0]const u8) std.math.Order {
    return boundedOrderIgnoreCaseZ(lhs, rhs, std.math.maxInt(usize));
}

test orderIgnoreCaseZ {
    try std.testing.expect(orderIgnoreCaseZ("aBcD", "Bee") == .lt);
    try std.testing.expect(orderIgnoreCaseZ("AbC", "aBc") == .eq);
    try std.testing.expect(orderIgnoreCaseZ("abC", "aBc0") == .lt);
    try std.testing.expect(orderIgnoreCaseZ("", "") == .eq);
    try std.testing.expect(orderIgnoreCaseZ("", "a") == .lt);

    const s: [*:0]const u8 = "Abc";
    try std.testing.expect(orderIgnoreCaseZ(s, s) == .eq);
}

/// Returns the lexicographical order of two many-item pointers with NUL-termination until some specified bound. O(n).
pub fn boundedOrderIgnoreCaseZ(lhs: [*:0]const u8, rhs: [*:0]const u8, bound: usize) std.math.Order {
    if (lhs == rhs) return .eq;
    var i: usize = 0;
    while (i < bound and toLower(lhs[i]) == toLower(rhs[i]) and lhs[i] != 0) : (i += 1) {}
    return if (i < bound) std.math.order(toLower(lhs[i]), toLower(rhs[i])) else .eq;
}

/// Returns whether the lexicographical order of `lhs` is lower than `rhs`.
pub fn lessThanIgnoreCase(lhs: []const u8, rhs: []const u8) bool {
    return orderIgnoreCase(lhs, rhs) == .lt;
}

pub const HexEscape = struct {
    bytes: []const u8,
    charset: *const [16]u8,

    pub const upper_charset = "0123456789ABCDEF";
    pub const lower_charset = "0123456789abcdef";

    pub fn format(se: HexEscape, w: *std.Io.Writer) std.Io.Writer.Error!void {
        const charset = se.charset;

        var buf: [4]u8 = undefined;
        buf[0] = '\\';
        buf[1] = 'x';

        for (se.bytes) |c| {
            if (std.ascii.isPrint(c)) {
                try w.writeByte(c);
            } else {
                buf[2] = charset[c >> 4];
                buf[3] = charset[c & 15];
                try w.writeAll(&buf);
            }
        }
    }
};

/// Replaces non-ASCII bytes with hex escapes.
pub fn hexEscape(bytes: []const u8, case: std.fmt.Case) std.fmt.Alt(HexEscape, HexEscape.format) {
    return .{ .data = .{ .bytes = bytes, .charset = switch (case) {
        .lower => HexEscape.lower_charset,
        .upper => HexEscape.upper_charset,
    } } };
}

test hexEscape {
    try std.testing.expectFmt("abc 123", "{f}", .{hexEscape("abc 123", .lower)});
    try std.testing.expectFmt("ab\\xffc", "{f}", .{hexEscape("ab\xffc", .lower)});
    try std.testing.expectFmt("abc 123", "{f}", .{hexEscape("abc 123", .upper)});
    try std.testing.expectFmt("ab\\xFFc", "{f}", .{hexEscape("ab\xffc", .upper)});
}



---
File: /std/atomic.zig
---

const builtin = @import("builtin");

const std = @import("std.zig");
const AtomicOrder = std.builtin.AtomicOrder;
const testing = std.testing;
const assert = std.debug.assert;

/// This is a thin wrapper around a primitive value to prevent accidental data races.
pub fn Value(comptime T: type) type {
    return extern struct {
        /// Care must be taken to avoid data races when interacting with this field directly.
        raw: T,

        const Self = @This();

        pub fn init(value: T) Self {
            return .{ .raw = value };
        }

        pub inline fn load(self: *const Self, comptime order: AtomicOrder) T {
            return @atomicLoad(T, &self.raw, order);
        }

        pub inline fn store(self: *Self, value: T, comptime order: AtomicOrder) void {
            @atomicStore(T, &self.raw, value, order);
        }

        pub inline fn swap(self: *Self, operand: T, comptime order: AtomicOrder) T {
            return @atomicRmw(T, &self.raw, .Xchg, operand, order);
        }

        pub inline fn cmpxchgWeak(
            self: *Self,
            expected_value: T,
            new_value: T,
            comptime success_order: AtomicOrder,
            comptime fail_order: AtomicOrder,
        ) ?T {
            return @cmpxchgWeak(T, &self.raw, expected_value, new_value, success_order, fail_order);
        }

        pub inline fn cmpxchgStrong(
            self: *Self,
            expected_value: T,
            new_value: T,
            comptime success_order: AtomicOrder,
            comptime fail_order: AtomicOrder,
        ) ?T {
            return @cmpxchgStrong(T, &self.raw, expected_value, new_value, success_order, fail_order);
        }

        pub inline fn fetchAdd(self: *Self, operand: T, comptime order: AtomicOrder) T {
            return @atomicRmw(T, &self.raw, .Add, operand, order);
        }

        pub inline fn fetchSub(self: *Self, operand: T, comptime order: AtomicOrder) T {
            return @atomicRmw(T, &self.raw, .Sub, operand, order);
        }

        pub inline fn fetchMin(self: *Self, operand: T, comptime order: AtomicOrder) T {
            return @atomicRmw(T, &self.raw, .Min, operand, order);
        }

        pub inline fn fetchMax(self: *Self, operand: T, comptime order: AtomicOrder) T {
            return @atomicRmw(T, &self.raw, .Max, operand, order);
        }

        pub inline fn fetchAnd(self: *Self, operand: T, comptime order: AtomicOrder) T {
            return @atomicRmw(T, &self.raw, .And, operand, order);
        }

        pub inline fn fetchNand(self: *Self, operand: T, comptime order: AtomicOrder) T {
            return @atomicRmw(T, &self.raw, .Nand, operand, order);
        }

        pub inline fn fetchXor(self: *Self, operand: T, comptime order: AtomicOrder) T {
            return @atomicRmw(T, &self.raw, .Xor, operand, order);
        }

        pub inline fn fetchOr(self: *Self, operand: T, comptime order: AtomicOrder) T {
            return @atomicRmw(T, &self.raw, .Or, operand, order);
        }

        pub inline fn rmw(
            self: *Self,
            comptime op: std.builtin.AtomicRmwOp,
            operand: T,
            comptime order: AtomicOrder,
        ) T {
            return @atomicRmw(T, &self.raw, op, operand, order);
        }

        const Bit = std.math.Log2Int(T);

        /// Marked `inline` so that if `bit` is comptime-known, the instruction
        /// can be lowered to a more efficient machine code instruction if
        /// possible.
        pub inline fn bitSet(self: *Self, bit: Bit, comptime order: AtomicOrder) u1 {
            const mask = @as(T, 1) << bit;
            const value = self.fetchOr(mask, order);
            return @intFromBool(value & mask != 0);
        }

        /// Marked `inline` so that if `bit` is comptime-known, the instruction
        /// can be lowered to a more efficient machine code instruction if
        /// possible.
        pub inline fn bitReset(self: *Self, bit: Bit, comptime order: AtomicOrder) u1 {
            const mask = @as(T, 1) << bit;
            const value = self.fetchAnd(~mask, order);
            return @intFromBool(value & mask != 0);
        }

        /// Marked `inline` so that if `bit` is comptime-known, the instruction
        /// can be lowered to a more efficient machine code instruction if
        /// possible.
        pub inline fn bitToggle(self: *Self, bit: Bit, comptime order: AtomicOrder) u1 {
            const mask = @as(T, 1) << bit;
            const value = self.fetchXor(mask, order);
            return @intFromBool(value & mask != 0);
        }
    };
}

test Value {
    const RefCount = struct {
        count: Value(usize),
        dropFn: *const fn (*RefCount) void,

        const RefCount = @This();

        fn ref(rc: *RefCount) void {
            // no synchronization necessary; just updating a counter.
            _ = rc.count.fetchAdd(1, .monotonic);
        }

        fn unref(rc: *RefCount) void {
            // release ensures code before unref() happens-before the
            // count is decremented as dropFn could be called by then.
            if (rc.count.fetchSub(1, .release) == 1) {
                // seeing 1 in the counter means that other unref()s have happened,
                // but it doesn't mean that uses before each unref() are visible.
                // The load acquires the release-sequence created by previous unref()s
                // in order to ensure visibility of uses before dropping.
                _ = rc.count.load(.acquire);
                (rc.dropFn)(rc);
            }
        }

        fn noop(rc: *RefCount) void {
            _ = rc;
        }
    };

    var ref_count: RefCount = .{
        .count = Value(usize).init(0),
        .dropFn = RefCount.noop,
    };
    ref_count.ref();
    ref_count.unref();
}

test "Value.swap" {
    var x = Value(usize).init(5);
    try testing.expectEqual(@as(usize, 5), x.swap(10, .seq_cst));
    try testing.expectEqual(@as(usize, 10), x.load(.seq_cst));

    const E = enum(usize) { a, b, c };
    var y = Value(E).init(.c);
    try testing.expectEqual(E.c, y.swap(.a, .seq_cst));
    try testing.expectEqual(E.a, y.load(.seq_cst));

    var z = Value(f32).init(5.0);
    try testing.expectEqual(@as(f32, 5.0), z.swap(10.0, .seq_cst));
    try testing.expectEqual(@as(f32, 10.0), z.load(.seq_cst));

    var a = Value(bool).init(false);
    try testing.expectEqual(false, a.swap(true, .seq_cst));
    try testing.expectEqual(true, a.load(.seq_cst));

    var b = Value(?*u8).init(null);
    try testing.expectEqual(@as(?*u8, null), b.swap(@as(?*u8, @ptrFromInt(@alignOf(u8))), .seq_cst));
    try testing.expectEqual(@as(?*u8, @ptrFromInt(@alignOf(u8))), b.load(.seq_cst));
}

test "Value.store" {
    var x = Value(usize).init(5);
    x.store(10, .seq_cst);
    try testing.expectEqual(@as(usize, 10), x.load(.seq_cst));
}

test "Value.cmpxchgWeak" {
    var x = Value(usize).init(0);

    try testing.expectEqual(@as(?usize, 0), x.cmpxchgWeak(1, 0, .seq_cst, .seq_cst));
    try testing.expectEqual(@as(usize, 0), x.load(.seq_cst));

    while (x.cmpxchgWeak(0, 1, .seq_cst, .seq_cst)) |_| {}
    try testing.expectEqual(@as(usize, 1), x.load(.seq_cst));

    while (x.cmpxchgWeak(1, 0, .seq_cst, .seq_cst)) |_| {}
    try testing.expectEqual(@as(usize, 0), x.load(.seq_cst));
}

test "Value.cmpxchgStrong" {
    var x = Value(usize).init(0);
    try testing.expectEqual(@as(?usize, 0), x.cmpxchgStrong(1, 0, .seq_cst, .seq_cst));
    try testing.expectEqual(@as(usize, 0), x.load(.seq_cst));
    try testing.expectEqual(@as(?usize, null), x.cmpxchgStrong(0, 1, .seq_cst, .seq_cst));
    try testing.expectEqual(@as(usize, 1), x.load(.seq_cst));
    try testing.expectEqual(@as(?usize, null), x.cmpxchgStrong(1, 0, .seq_cst, .seq_cst));
    try testing.expectEqual(@as(usize, 0), x.load(.seq_cst));
}

test "Value.fetchAdd" {
    var x = Value(usize).init(5);
    try testing.expectEqual(@as(usize, 5), x.fetchAdd(5, .seq_cst));
    try testing.expectEqual(@as(usize, 10), x.load(.seq_cst));
    try testing.expectEqual(@as(usize, 10), x.fetchAdd(std.math.maxInt(usize), .seq_cst));
    try testing.expectEqual(@as(usize, 9), x.load(.seq_cst));
}

test "Value.fetchSub" {
    var x = Value(usize).init(5);
    try testing.expectEqual(@as(usize, 5), x.fetchSub(5, .seq_cst));
    try testing.expectEqual(@as(usize, 0), x.load(.seq_cst));
    try testing.expectEqual(@as(usize, 0), x.fetchSub(1, .seq_cst));
    try testing.expectEqual(@as(usize, std.math.maxInt(usize)), x.load(.seq_cst));
}

test "Value.fetchMin" {
    var x = Value(usize).init(5);
    try testing.expectEqual(@as(usize, 5), x.fetchMin(0, .seq_cst));
    try testing.expectEqual(@as(usize, 0), x.load(.seq_cst));
    try testing.expectEqual(@as(usize, 0), x.fetchMin(10, .seq_cst));
    try testing.expectEqual(@as(usize, 0), x.load(.seq_cst));
}

test "Value.fetchMax" {
    var x = Value(usize).init(5);
    try testing.expectEqual(@as(usize, 5), x.fetchMax(10, .seq_cst));
    try testing.expectEqual(@as(usize, 10), x.load(.seq_cst));
    try testing.expectEqual(@as(usize, 10), x.fetchMax(5, .seq_cst));
    try testing.expectEqual(@as(usize, 10), x.load(.seq_cst));
}

test "Value.fetchAnd" {
    var x = Value(usize).init(0b11);
    try testing.expectEqual(@as(usize, 0b11), x.fetchAnd(0b10, .seq_cst));
    try testing.expectEqual(@as(usize, 0b10), x.load(.seq_cst));
    try testing.expectEqual(@as(usize, 0b10), x.fetchAnd(0b00, .seq_cst));
    try testing.expectEqual(@as(usize, 0b00), x.load(.seq_cst));
}

test "Value.fetchNand" {
    var x = Value(usize).init(0b11);
    try testing.expectEqual(@as(usize, 0b11), x.fetchNand(0b10, .seq_cst));
    try testing.expectEqual(~@as(usize, 0b10), x.load(.seq_cst));
    try testing.expectEqual(~@as(usize, 0b10), x.fetchNand(0b00, .seq_cst));
    try testing.expectEqual(~@as(usize, 0b00), x.load(.seq_cst));
}

test "Value.fetchOr" {
    var x = Value(usize).init(0b11);
    try testing.expectEqual(@as(usize, 0b11), x.fetchOr(0b100, .seq_cst));
    try testing.expectEqual(@as(usize, 0b111), x.load(.seq_cst));
    try testing.expectEqual(@as(usize, 0b111), x.fetchOr(0b010, .seq_cst));
    try testing.expectEqual(@as(usize, 0b111), x.load(.seq_cst));
}

test "Value.fetchXor" {
    var x = Value(usize).init(0b11);
    try testing.expectEqual(@as(usize, 0b11), x.fetchXor(0b10, .seq_cst));
    try testing.expectEqual(@as(usize, 0b01), x.load(.seq_cst));
    try testing.expectEqual(@as(usize, 0b01), x.fetchXor(0b01, .seq_cst));
    try testing.expectEqual(@as(usize, 0b00), x.load(.seq_cst));
}

test "Value.bitSet" {
    var x = Value(usize).init(0);

    for (0..@bitSizeOf(usize)) |bit_index| {
        const bit = @as(std.math.Log2Int(usize), @intCast(bit_index));
        const mask = @as(usize, 1) << bit;

        // setting the bit should change the bit
        try testing.expect(x.load(.seq_cst) & mask == 0);
        try testing.expectEqual(@as(u1, 0), x.bitSet(bit, .seq_cst));
        try testing.expect(x.load(.seq_cst) & mask != 0);

        // setting it again shouldn't change the bit
        try testing.expectEqual(@as(u1, 1), x.bitSet(bit, .seq_cst));
        try testing.expect(x.load(.seq_cst) & mask != 0);

        // all the previous bits should have not changed (still be set)
        for (0..bit_index) |prev_bit_index| {
            const prev_bit = @as(std.math.Log2Int(usize), @intCast(prev_bit_index));
            const prev_mask = @as(usize, 1) << prev_bit;
            try testing.expect(x.load(.seq_cst) & prev_mask != 0);
        }
    }
}

test "Value.bitReset" {
    var x = Value(usize).init(0);

    for (0..@bitSizeOf(usize)) |bit_index| {
        const bit = @as(std.math.Log2Int(usize), @intCast(bit_index));
        const mask = @as(usize, 1) << bit;
        x.raw |= mask;

        // unsetting the bit should change the bit
        try testing.expect(x.load(.seq_cst) & mask != 0);
        try testing.expectEqual(@as(u1, 1), x.bitReset(bit, .seq_cst));
        try testing.expect(x.load(.seq_cst) & mask == 0);

        // unsetting it again shouldn't change the bit
        try testing.expectEqual(@as(u1, 0), x.bitReset(bit, .seq_cst));
        try testing.expect(x.load(.seq_cst) & mask == 0);

        // all the previous bits should have not changed (still be reset)
        for (0..bit_index) |prev_bit_index| {
            const prev_bit = @as(std.math.Log2Int(usize), @intCast(prev_bit_index));
            const prev_mask = @as(usize, 1) << prev_bit;
            try testing.expect(x.load(.seq_cst) & prev_mask == 0);
        }
    }
}

test "Value.bitToggle" {
    var x = Value(usize).init(0);

    for (0..@bitSizeOf(usize)) |bit_index| {
        const bit = @as(std.math.Log2Int(usize), @intCast(bit_index));
        const mask = @as(usize, 1) << bit;

        // toggling the bit should change the bit
        try testing.expect(x.load(.seq_cst) & mask == 0);
        try testing.expectEqual(@as(u1, 0), x.bitToggle(bit, .seq_cst));
        try testing.expect(x.load(.seq_cst) & mask != 0);

        // toggling it again *should* change the bit
        try testing.expectEqual(@as(u1, 1), x.bitToggle(bit, .seq_cst));
        try testing.expect(x.load(.seq_cst) & mask == 0);

        // all the previous bits should have not changed (still be toggled back)
        for (0..bit_index) |prev_bit_index| {
            const prev_bit = @as(std.math.Log2Int(usize), @intCast(prev_bit_index));
            const prev_mask = @as(usize, 1) << prev_bit;
            try testing.expect(x.load(.seq_cst) & prev_mask == 0);
        }
    }
}

/// Signals to the processor that the caller is inside a busy-wait spin-loop.
pub inline fn spinLoopHint() void {
    switch (builtin.target.cpu.arch) {
        // No-op instruction that can hint to save (or share with a hardware-thread)
        // pipelining/power resources
        // https://software.intel.com/content/www/us/en/develop/articles/benefitting-power-and-performance-sleep-loops.html
        .x86,
        .x86_64,
        => asm volatile ("pause"),

        // No-op instruction that serves as a hardware-thread resource yield hint.
        // https://stackoverflow.com/a/7588941
        .powerpc,
        .powerpcle,
        .powerpc64,
        .powerpc64le,
        => asm volatile ("or 27, 27, 27"),

        // `isb` appears more reliable for releasing execution resources than `yield`
        // on common aarch64 CPUs.
        // https://bugs.java.com/bugdatabase/view_bug.do?bug_id=8258604
        // https://bugs.mysql.com/bug.php?id=100664
        .aarch64,
        .aarch64_be,
        => asm volatile ("isb"),

        // `yield` was introduced in v6k but is also available on v6m.
        // https://www.keil.com/support/man/docs/armasm/armasm_dom1361289926796.htm
        .arm,
        .armeb,
        .thumb,
        .thumbeb,
        => if (comptime builtin.cpu.hasAny(.arm, &.{ .has_v6k, .has_v6m })) {
            asm volatile ("yield");
        },

        // The 8-bit immediate specifies the amount of cycles to pause for. We can't really be too
        // opinionated here.
        .hexagon,
        => asm volatile ("pause(#1)"),

        .riscv32,
        .riscv32be,
        .riscv64,
        .riscv64be,
        => if (comptime builtin.cpu.has(.riscv, .zihintpause)) {
            asm volatile ("pause");
        },

        else => {},
    }
}

test spinLoopHint {
    for (0..10) |_| {
        spinLoopHint();
    }
}

pub fn cacheLineForCpu(cpu: std.Target.Cpu) u16 {
    return switch (cpu.arch) {
        // x86_64: Starting from Intel's Sandy Bridge, the spatial prefetcher pulls in pairs of 64-byte cache lines at a time.
        // - https://www.intel.com/content/dam/www/public/us/en/documents/manuals/64-ia-32-architectures-optimization-manual.pdf
        // - https://github.com/facebook/folly/blob/1b5288e6eea6df074758f877c849b6e73bbb9fbb/folly/lang/Align.h#L107
        //
        // aarch64: Some big.LITTLE ARM archs have "big" cores with 128-byte cache lines:
        // - https://www.mono-project.com/news/2016/09/12/arm64-icache/
        // - https://cpufun.substack.com/p/more-m1-fun-hardware-information
        //
        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/arc/Kconfig#L212
        // - https://github.com/golang/go/blob/3dd58676054223962cd915bb0934d1f9f489d4d2/src/internal/cpu/cpu_ppc64x.go#L9
        .x86_64,
        .aarch64,
        .aarch64_be,
        .arc,
        .arceb,
        .powerpc64,
        .powerpc64le,
        => 128,

        // https://github.com/llvm/llvm-project/blob/e379094328e49731a606304f7e3559d4f1fa96f9/clang/lib/Basic/Targets/Hexagon.h#L145-L151
        .hexagon,
        => if (cpu.has(.hexagon, .v73)) 64 else 32,

        // - https://github.com/golang/go/blob/3dd58676054223962cd915bb0934d1f9f489d4d2/src/internal/cpu/cpu_arm.go#L7
        // - https://github.com/golang/go/blob/3dd58676054223962cd915bb0934d1f9f489d4d2/src/
```
