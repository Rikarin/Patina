from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_history_state import GraphHistoryState
  from ..models.stack_anchor import StackAnchor
  from ..models.stack_channel import StackChannel
  from ..models.stack_evaluation import StackEvaluation
  from ..models.stack_layer import StackLayer
  from ..models.stack_pace import StackPace
  from ..models.stack_preview import StackPreview





T = TypeVar("T", bound="StackState")



@_attrs_define
class StackState:
    """ A whole stack: everything a client needs to draw it, and no pixels.

        Attributes:
            id (str): The session.
            path (str): Its file.
            name (str): What it is called.
            set_ (str): The texture set this session paints.
            width (int): Its width in texels.
            height (int): Its height in texels.
            revision (int): The revision of the document.
            sequence (int): The number of the last event it includes.
            history (GraphHistoryState): What the undo history offers.
            channels (list[StackChannel]): The channels it writes.
            layers (list[StackLayer]): Its layers, bottom first.
            previews (list[StackPreview]): The newest maps.
            settled (int): The newest revision an evaluation has reached.
            model (str): The mesh it is painted on, as the document names it, or empty.
            seed (int): The number every procedural step derives from. Default: 0.
            height_normal (str): How a height channel becomes the set's normal: `Sharp` or `Smooth`. Read only when the set
                has a
                height channel, and then it writes a normal whether or not it declares one. Default: 'Smooth'.
            uv_set (int): The mesh's coordinate set the set is painted, baked and drawn in, by the file's number. Default:
                0.
            baked_normal (str): How the bake's normal reaches the set: `Combine`, `Replace`, `Off`. Default: 'Combine'.
            baked_occlusion (str): How its occlusion does: `Combine`, `Replace`, `Off`. Default: 'Combine'.
            evaluation (None | StackEvaluation | Unset):
            mesh_maps (None | str | Unset): The bake its generators read, or null.
            anchors (list[StackAnchor] | None | Unset): The anchor points its set publishes, by name, and who reads each.
            bake_stale (None | str | Unset): Why the bake the set reads no longer fits it, or null when it does or there is
                none: `mesh`, made on a mesh
                the set no longer stands on, or `uvSet`, laid out in another of the mesh's UV sets (#238), or `shape`,
                of another shape than the set's Width by Height (#262). Either way its maps need baking again.
            pace (None | StackPace | Unset):
     """

    id: str
    path: str
    name: str
    set_: str
    width: int
    height: int
    revision: int
    sequence: int
    history: GraphHistoryState
    channels: list[StackChannel]
    layers: list[StackLayer]
    previews: list[StackPreview]
    settled: int
    model: str
    seed: int = 0
    height_normal: str = 'Smooth'
    uv_set: int = 0
    baked_normal: str = 'Combine'
    baked_occlusion: str = 'Combine'
    evaluation: None | StackEvaluation | Unset = UNSET
    mesh_maps: None | str | Unset = UNSET
    anchors: list[StackAnchor] | None | Unset = UNSET
    bake_stale: None | str | Unset = UNSET
    pace: None | StackPace | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.stack_anchor import StackAnchor # noqa: PLC0415
        from ..models.stack_channel import StackChannel # noqa: PLC0415
        from ..models.stack_evaluation import StackEvaluation # noqa: PLC0415
        from ..models.stack_layer import StackLayer # noqa: PLC0415
        from ..models.stack_pace import StackPace # noqa: PLC0415
        from ..models.stack_preview import StackPreview # noqa: PLC0415
        id = self.id

        path = self.path

        name = self.name

        set_ = self.set_

        width = self.width

        height = self.height

        revision = self.revision

        sequence = self.sequence

        history = self.history.to_dict()

        channels = []
        for channels_item_data in self.channels:
            channels_item = channels_item_data.to_dict()
            channels.append(channels_item)



        layers = []
        for layers_item_data in self.layers:
            layers_item = layers_item_data.to_dict()
            layers.append(layers_item)



        previews = []
        for previews_item_data in self.previews:
            previews_item = previews_item_data.to_dict()
            previews.append(previews_item)



        settled = self.settled

        model = self.model

        seed = self.seed

        height_normal = self.height_normal

        uv_set = self.uv_set

        baked_normal = self.baked_normal

        baked_occlusion = self.baked_occlusion

        evaluation: dict[str, Any] | None | Unset
        if isinstance(self.evaluation, Unset):
            evaluation = UNSET
        elif isinstance(self.evaluation, StackEvaluation):
            evaluation = self.evaluation.to_dict()
        else:
            evaluation = self.evaluation

        mesh_maps: None | str | Unset
        if isinstance(self.mesh_maps, Unset):
            mesh_maps = UNSET
        else:
            mesh_maps = self.mesh_maps

        anchors: list[dict[str, Any]] | None | Unset
        if isinstance(self.anchors, Unset):
            anchors = UNSET
        elif isinstance(self.anchors, list):
            anchors = []
            for anchors_type_1_item_data in self.anchors:
                anchors_type_1_item = anchors_type_1_item_data.to_dict()
                anchors.append(anchors_type_1_item)


        else:
            anchors = self.anchors

        bake_stale: None | str | Unset
        if isinstance(self.bake_stale, Unset):
            bake_stale = UNSET
        else:
            bake_stale = self.bake_stale

        pace: dict[str, Any] | None | Unset
        if isinstance(self.pace, Unset):
            pace = UNSET
        elif isinstance(self.pace, StackPace):
            pace = self.pace.to_dict()
        else:
            pace = self.pace


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "path": path,
            "name": name,
            "set": set_,
            "width": width,
            "height": height,
            "revision": revision,
            "sequence": sequence,
            "history": history,
            "channels": channels,
            "layers": layers,
            "previews": previews,
            "settled": settled,
            "model": model,
            "seed": seed,
            "heightNormal": height_normal,
            "uvSet": uv_set,
            "bakedNormal": baked_normal,
            "bakedOcclusion": baked_occlusion,
        })
        if evaluation is not UNSET:
            field_dict["evaluation"] = evaluation
        if mesh_maps is not UNSET:
            field_dict["meshMaps"] = mesh_maps
        if anchors is not UNSET:
            field_dict["anchors"] = anchors
        if bake_stale is not UNSET:
            field_dict["bakeStale"] = bake_stale
        if pace is not UNSET:
            field_dict["pace"] = pace

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.stack_anchor import StackAnchor # noqa: PLC0415
        from ..models.stack_channel import StackChannel # noqa: PLC0415
        from ..models.stack_evaluation import StackEvaluation # noqa: PLC0415
        from ..models.stack_layer import StackLayer # noqa: PLC0415
        from ..models.stack_pace import StackPace # noqa: PLC0415
        from ..models.stack_preview import StackPreview # noqa: PLC0415
        d = dict(src_dict)
        id = d.pop("id")

        path = d.pop("path")

        name = d.pop("name")

        set_ = d.pop("set")

        width = d.pop("width")

        height = d.pop("height")

        revision = d.pop("revision")

        sequence = d.pop("sequence")

        history = GraphHistoryState.from_dict(d.pop("history"))




        channels = []
        _channels = d.pop("channels")
        for channels_item_data in (_channels):
            channels_item = StackChannel.from_dict(channels_item_data)



            channels.append(channels_item)


        layers = []
        _layers = d.pop("layers")
        for layers_item_data in (_layers):
            layers_item = StackLayer.from_dict(layers_item_data)



            layers.append(layers_item)


        previews = []
        _previews = d.pop("previews")
        for previews_item_data in (_previews):
            previews_item = StackPreview.from_dict(previews_item_data)



            previews.append(previews_item)


        settled = d.pop("settled")

        model = d.pop("model")

        seed = d.pop("seed")

        height_normal = d.pop("heightNormal")

        uv_set = d.pop("uvSet")

        baked_normal = d.pop("bakedNormal")

        baked_occlusion = d.pop("bakedOcclusion")

        def _parse_evaluation(data: object) -> None | StackEvaluation | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                evaluation_type_1 = StackEvaluation.from_dict(data)



                return evaluation_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackEvaluation | Unset, data)

        evaluation = _parse_evaluation(d.pop("evaluation", UNSET))


        def _parse_mesh_maps(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        mesh_maps = _parse_mesh_maps(d.pop("meshMaps", UNSET))


        def _parse_anchors(data: object) -> list[StackAnchor] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                anchors_type_1 = []
                _anchors_type_1 = data
                for anchors_type_1_item_data in (_anchors_type_1):
                    anchors_type_1_item = StackAnchor.from_dict(anchors_type_1_item_data)



                    anchors_type_1.append(anchors_type_1_item)

                return anchors_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[StackAnchor] | None | Unset, data)

        anchors = _parse_anchors(d.pop("anchors", UNSET))


        def _parse_bake_stale(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        bake_stale = _parse_bake_stale(d.pop("bakeStale", UNSET))


        def _parse_pace(data: object) -> None | StackPace | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                pace_type_1 = StackPace.from_dict(data)



                return pace_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackPace | Unset, data)

        pace = _parse_pace(d.pop("pace", UNSET))


        stack_state = cls(
            id=id,
            path=path,
            name=name,
            set_=set_,
            width=width,
            height=height,
            revision=revision,
            sequence=sequence,
            history=history,
            channels=channels,
            layers=layers,
            previews=previews,
            settled=settled,
            model=model,
            seed=seed,
            height_normal=height_normal,
            uv_set=uv_set,
            baked_normal=baked_normal,
            baked_occlusion=baked_occlusion,
            evaluation=evaluation,
            mesh_maps=mesh_maps,
            anchors=anchors,
            bake_stale=bake_stale,
            pace=pace,
        )


        stack_state.additional_properties = d
        return stack_state

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
