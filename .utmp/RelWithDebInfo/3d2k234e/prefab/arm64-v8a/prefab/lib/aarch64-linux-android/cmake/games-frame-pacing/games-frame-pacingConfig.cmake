if(NOT TARGET games-frame-pacing::swappy)
add_library(games-frame-pacing::swappy SHARED IMPORTED)
set_target_properties(games-frame-pacing::swappy PROPERTIES
    IMPORTED_LOCATION "/home/wewantgreatness/.gradle/caches/8.13/transforms/2d0dff526b5037728ef71c5a0bb8e7f7/transformed/jetified-games-frame-pacing-2.1.2/prefab/modules/swappy/libs/android.arm64-v8a/libswappy.so"
    INTERFACE_INCLUDE_DIRECTORIES "/home/wewantgreatness/.gradle/caches/8.13/transforms/2d0dff526b5037728ef71c5a0bb8e7f7/transformed/jetified-games-frame-pacing-2.1.2/prefab/modules/swappy/include"
    INTERFACE_LINK_LIBRARIES ""
)
endif()

if(NOT TARGET games-frame-pacing::swappy_static)
add_library(games-frame-pacing::swappy_static STATIC IMPORTED)
set_target_properties(games-frame-pacing::swappy_static PROPERTIES
    IMPORTED_LOCATION "/home/wewantgreatness/.gradle/caches/8.13/transforms/2d0dff526b5037728ef71c5a0bb8e7f7/transformed/jetified-games-frame-pacing-2.1.2/prefab/modules/swappy_static/libs/android.arm64-v8a/libswappy_static.a"
    INTERFACE_INCLUDE_DIRECTORIES "/home/wewantgreatness/.gradle/caches/8.13/transforms/2d0dff526b5037728ef71c5a0bb8e7f7/transformed/jetified-games-frame-pacing-2.1.2/prefab/modules/swappy_static/include"
    INTERFACE_LINK_LIBRARIES ""
)
endif()

