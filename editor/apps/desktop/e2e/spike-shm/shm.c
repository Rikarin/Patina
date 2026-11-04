// SPDX-License-Identifier: GPL-3.0-or-later
// Spike S3 only: a file mapping for a renderer, copied into a typed array. Not part of the app (ADR 0003).
#include <fcntl.h>
#include <node_api.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <unistd.h>

static unsigned char *mapping;

static napi_value open_file(napi_env env, napi_callback_info info) {
  size_t argc = 2;
  napi_value argv[2];
  napi_get_cb_info(env, info, &argc, argv, NULL, NULL);
  char path[1024];
  size_t length;
  double size;
  napi_get_value_string_utf8(env, argv[0], path, sizeof path, &length);
  napi_get_value_double(env, argv[1], &size);
  int file = open(path, O_RDWR);
  mapping = mmap(NULL, (size_t)size, PROT_READ | PROT_WRITE, MAP_SHARED, file, 0);
  close(file);
  return NULL;
}

/** copy(offset, typedArray): one memcpy from the mapping into memory V8 owns. */
static napi_value copy(napi_env env, napi_callback_info info) {
  size_t argc = 2;
  napi_value argv[2];
  napi_get_cb_info(env, info, &argc, argv, NULL, NULL);
  double offset;
  void *target;
  size_t length;
  napi_get_value_double(env, argv[0], &offset);
  napi_get_typedarray_info(env, argv[1], NULL, &length, &target, NULL, NULL);
  memcpy(target, mapping + (size_t)offset, length);
  return NULL;
}

/** wrap(offset, length): the zero-copy attempt. Says what Electron answers instead of an ArrayBuffer over the mapping. */
static napi_value wrap(napi_env env, napi_callback_info info) {
  size_t argc = 2;
  napi_value argv[2];
  napi_get_cb_info(env, info, &argc, argv, NULL, NULL);
  double offset, length;
  napi_get_value_double(env, argv[0], &offset);
  napi_get_value_double(env, argv[1], &length);
  napi_value buffer;
  napi_status status =
      napi_create_external_arraybuffer(env, mapping + (size_t)offset, (size_t)length, NULL, NULL, &buffer);
  napi_value answer;
  napi_create_int32(env, status, &answer);
  return answer;
}

NAPI_MODULE_INIT() {
  napi_value f;
  napi_create_function(env, "open", NAPI_AUTO_LENGTH, open_file, NULL, &f);
  napi_set_named_property(env, exports, "open", f);
  napi_create_function(env, "copy", NAPI_AUTO_LENGTH, copy, NULL, &f);
  napi_set_named_property(env, exports, "copy", f);
  napi_create_function(env, "wrap", NAPI_AUTO_LENGTH, wrap, NULL, &f);
  napi_set_named_property(env, exports, "wrap", f);
  return exports;
}
