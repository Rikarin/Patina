from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.stack_pixels import StackPixels
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    id: str,
    *,
    clean: bool | Unset = False,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["clean"] = clean


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/stacks/{id}/pixels".format(id=quote(str(id), safe=""),),
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | list[StackPixels] | None:
    if response.status_code == 200:
        response_200 = []
        _response_200 = response.json()
        for response_200_item_data in (_response_200):
            response_200_item = StackPixels.from_dict(response_200_item_data)



            response_200.append(response_200_item)

        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 503:
        response_503 = ProblemDetails.from_dict(response.json())



        return response_503

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails | list[StackPixels]]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    clean: bool | Unset = False,

) -> Response[ProblemDetails | list[StackPixels]]:
    """ A digest of each map as it stands, the same one an export reports, with no pixels in the reply.

    Args:
        id (str):
        clean (bool | Unset):  Default: False.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | list[StackPixels]]
     """


    kwargs = _get_kwargs(
        id=id,
clean=clean,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    clean: bool | Unset = False,

) -> ProblemDetails | list[StackPixels] | None:
    """ A digest of each map as it stands, the same one an export reports, with no pixels in the reply.

    Args:
        id (str):
        clean (bool | Unset):  Default: False.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | list[StackPixels]
     """


    return sync_detailed(
        id=id,
client=client,
clean=clean,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    clean: bool | Unset = False,

) -> Response[ProblemDetails | list[StackPixels]]:
    """ A digest of each map as it stands, the same one an export reports, with no pixels in the reply.

    Args:
        id (str):
        clean (bool | Unset):  Default: False.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | list[StackPixels]]
     """


    kwargs = _get_kwargs(
        id=id,
clean=clean,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    clean: bool | Unset = False,

) -> ProblemDetails | list[StackPixels] | None:
    """ A digest of each map as it stands, the same one an export reports, with no pixels in the reply.

    Args:
        id (str):
        clean (bool | Unset):  Default: False.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | list[StackPixels]
     """


    return (await asyncio_detailed(
        id=id,
client=client,
clean=clean,

    )).parsed
