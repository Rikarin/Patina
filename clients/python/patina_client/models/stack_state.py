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
  from ..models.stack_channel import StackChannel
  from ..models.stack_evaluation import StackEvaluation
  from ..models.stack_layer import StackLayer
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
            evaluation (None | StackEvaluation | Unset):
            mesh_maps (None | str | Unset): The bake its generators read, or null.
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
    evaluation: None | StackEvaluation | Unset = UNSET
    mesh_maps: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.stack_channel import StackChannel # noqa: PLC0415
        from ..models.stack_evaluation import StackEvaluation # noqa: PLC0415
        from ..models.stack_layer import StackLayer # noqa: PLC0415
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
        })
        if evaluation is not UNSET:
            field_dict["evaluation"] = evaluation
        if mesh_maps is not UNSET:
            field_dict["meshMaps"] = mesh_maps

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.stack_channel import StackChannel # noqa: PLC0415
        from ..models.stack_evaluation import StackEvaluation # noqa: PLC0415
        from ..models.stack_layer import StackLayer # noqa: PLC0415
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
            evaluation=evaluation,
            mesh_maps=mesh_maps,
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
